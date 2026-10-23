using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    /// <summary>
    /// Owns the AiJob queue: enqueueing a grading job per essay/coding answer
    /// when a student submits, and processing queued jobs one at a time
    /// (called repeatedly by AiJobProcessorService's background timer).
    ///
    /// The AI's result is written as a PRELIMINARY grade — StudentAnswer.ObtainedPoints
    /// gets set with GradedBy = "ai", but the attempt's Status is NOT touched here,
    /// so it stays "submitted" until an instructor reviews it in GradingTab and
    /// either edits it (GradedBy flips to "instructor") or clicks "Finalize Grades"
    /// (ExamStatusService.FinalizeExamGradesAsync), which is what actually locks
    /// scores in as final.
    /// </summary>
    public class AiJobService
    {
        // Dynamic Gemini model configured in Faculty Settings
        private static string GeminiModelName => AppSettingsService.GetGeminiModel();
        private const int MaxAttempts = 3; // retry a failed job up to this many times before giving up

        // If the app (or debugger) gets closed/restarted while a job is
        // mid-flight — e.g. during the Gemini HTTP call — nothing ever runs
        // the catch block, and the job stays stuck at Status="processing"
        // forever. ProcessNextQueuedJobAsync only ever looks at "queued" jobs,
        // so without this, an orphaned job requires manual SQL intervention
        // every time. This threshold is deliberately generous — a real Gemini
        // call plus PDF upload shouldn't take anywhere near 2 minutes.
        private static readonly TimeSpan StaleProcessingThreshold = TimeSpan.FromMinutes(2);

        // ── Called once per poller tick, before draining the queue ───────
        public async Task ReclaimStaleJobsAsync()
        {
            using var context = new AppDbContext();
            var cutoff = DateTime.Now - StaleProcessingThreshold;

            var staleJobs = await context.Set<AiJob>()
                .Where(j => j.Status == "processing" && j.StartedAt.HasValue && j.StartedAt.Value < cutoff)
                .ToListAsync();

            foreach (var job in staleJobs)
            {
                job.AttemptCount++;
                bool exhausted = job.AttemptCount >= MaxAttempts;

                job.Status = exhausted ? "failed" : "queued";
                job.StartedAt = null;
                job.LastError = exhausted
                    ? "Job was stuck in 'processing' (the app likely closed/restarted mid-grading) and has exhausted its retries."
                    : "Job was stuck in 'processing' (the app likely closed/restarted mid-grading) — reset to retry.";
            }

            if (staleJobs.Count > 0)
                await context.SaveChangesAsync();
        }

        // ── Called from ExamService.SubmitExamAsync ──────────────────────
        public async Task EnqueueGradingJobsForAttemptAsync(Guid attemptId)
        {
            using var context = new AppDbContext();

            Guid modelId = await EnsureGeminiModelAsync(context);

            var answers = await context.StudentAnswers
                .Include(a => a.Question)
                .Where(a => a.AttemptId == attemptId
                         && a.Question != null
                         && (a.Question.QuestionType == "essay" || a.Question.QuestionType == "coding"))
                .ToListAsync();

            bool anyChanges = false;

            foreach (var answer in answers)
            {
                bool alreadyQueued = await context.Set<AiJob>()
                    .AnyAsync(j => j.AnswerId == answer.AnswerId &&
                                   (j.Status == "queued" || j.Status == "processing"));

                if (alreadyQueued) continue;

                context.Add(new AiJob
                {
                    JobId = Guid.NewGuid(),
                    AnswerId = answer.AnswerId,
                    ModelId = modelId,
                    Status = "queued",
                    Priority = 5,
                    AttemptCount = 0,
                    CreatedAt = DateTime.Now
                });
                anyChanges = true;
            }

            if (anyChanges)
                await context.SaveChangesAsync();
        }

        // ── Called repeatedly by AiJobProcessorService's timer tick ──────
        // Claims and processes ONE queued job. Returns true if it did work,
        // so the caller can keep draining the queue until nothing's left.
        public async Task<bool> ProcessNextQueuedJobAsync()
        {
            using var context = new AppDbContext();

            var job = await context.Set<AiJob>()
                .Where(j => j.Status == "queued")
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefaultAsync();

            if (job == null) return false;

            // Claim immediately, before doing any slow work, so another app
            // instance polling concurrently (e.g. a different lab PC) is less
            // likely to grab the same job. Not fully race-proof under heavy
            // concurrency — acceptable for a lab-scale exam environment, not
            // a high-throughput production queue.
            job.Status = "processing";
            job.StartedAt = DateTime.Now;
            await context.SaveChangesAsync();

            try
            {
                await GradeOneAnswerAsync(context, job);

                job.Status = "completed";
                job.CompletedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                job.AttemptCount++;
                job.LastError = ex.Message;
                job.Status = job.AttemptCount >= MaxAttempts ? "failed" : "queued"; // retry unless exhausted
            }

            await context.SaveChangesAsync();
            return true;
        }

        // ── Direct retry triggered from manual "Retry AI Grading" UI button ──
        public async Task RetryAnswerGradingAsync(Guid answerId)
        {
            using var context = new AppDbContext();
            Guid modelId = await EnsureGeminiModelAsync(context);

            var job = await context.Set<AiJob>()
                .FirstOrDefaultAsync(j => j.AnswerId == answerId);

            if (job == null)
            {
                job = new AiJob
                {
                    JobId = Guid.NewGuid(),
                    AnswerId = answerId,
                    ModelId = modelId,
                    Status = "processing",
                    Priority = 1,
                    AttemptCount = 1,
                    StartedAt = DateTime.Now,
                    CreatedAt = DateTime.Now
                };
                context.Add(job);
            }
            else
            {
                job.ModelId = modelId;
                job.Status = "processing";
                job.StartedAt = DateTime.Now;
                job.LastError = null;
            }
            await context.SaveChangesAsync();

            try
            {
                await GradeOneAnswerAsync(context, job);
                job.Status = "completed";
                job.CompletedAt = DateTime.Now;
                job.LastError = null;
            }
            catch (Exception ex)
            {
                job.AttemptCount++;
                job.LastError = ex.Message;
                job.Status = "failed";
                await context.SaveChangesAsync();
                throw;
            }

            await context.SaveChangesAsync();
        }

        // ── The actual grading + write-back for one answer ───────────────
        private async Task GradeOneAnswerAsync(AppDbContext context, AiJob job)
        {
            var answer = await context.StudentAnswers
                .Include(a => a.Question)
                .FirstOrDefaultAsync(a => a.AnswerId == job.AnswerId);

            if (answer?.Question == null)
                throw new InvalidOperationException($"Answer {job.AnswerId} or its Question could not be found.");

            var question = answer.Question;

            var rubricService = new RubricService();
            var rubric = await rubricService.GetRubricForGradingAsync(question.QuestionId);

            if (rubric == null)
                throw new InvalidOperationException(
                    $"No rubric found for question {question.QuestionId} — cannot AI-grade without one.");

            bool isCode = question.QuestionType == "coding";
            string rawAnswer = isCode ? (answer.SourceCode ?? string.Empty) : (answer.AnswerText ?? string.Empty);

            var request = new RubricGradingRequest
            {
                QuestionText = question.Text,
                StudentAnswerText = TextSanitizer.SanitizeStudentInput(rawAnswer),
                IsCode = isCode,
                ProgrammingLanguage = question.ProgrammingLanguage,
                Criteria = rubric.Criteria,
                InstructorInstructions = rubric.InstructorInstructions,
                AttachmentPdfPath = rubric.AttachmentPath,
                ReferenceAnswer = question.CorrectAnswer
            };

            var gradingService = new GeminiRubricGradingService();
            var result = await gradingService.GradeAsync(request);

            // ── PRELIMINARY score — not final until an instructor finalizes ──
            // Gemini's TotalScore is on the RUBRIC's own scale (whatever the
            // criteria's points summed to in AIRubricModal — e.g. out of 10,
            // out of 100), which is NOT necessarily the same as this question's
            // actual point value (Question.Points). Scale proportionally so a
            // 1-point question doesn't end up with a score like "5.40".
            decimal scaledScore = result.MaxScore > 0 && question.Points.HasValue
                ? Math.Round((result.TotalScore / result.MaxScore) * question.Points.Value, 2)
                : result.TotalScore; // fallback: no question point value to scale against

            answer.ObtainedPoints = scaledScore;
            answer.GradedBy = "ai";
            answer.GradedAt = DateTime.Now;
            answer.IsManualGrade = false;

            var rubricEntity = await context.Set<Rubric>()
                .FirstOrDefaultAsync(r => r.QuestionId == question.QuestionId);

            // ── Full reasoning, for the instructor to review in GradingTab ──
            var evaluation = new AiEvaluation
            {
                EvaluationId = Guid.NewGuid(),
                AnswerId = answer.AnswerId,
                ModelId = job.ModelId,
                RubricUsedId = rubricEntity?.RubricId,
                EvaluatedAt = DateTime.Now,
                Score = scaledScore,
                ConfidenceScore = result.AlignedWithAttachment == false ? 0.78m : 0.94m,
                Feedback = result.OverallFeedback,
                AiResponseRaw = result.RawResponseJson
            };
            context.Add(evaluation);

            if (rubricEntity != null)
            {
                var criteriaEntities = await context.Set<RubricCriteria>()
                    .Include(c => c.Levels)
                    .Where(c => c.RubricId == rubricEntity.RubricId)
                    .ToListAsync();

                foreach (var criterionResult in result.CriteriaResults)
                {
                    // Gemini's response identifies criteria/tiers by NAME (it has no
                    // knowledge of our internal Guid IDs) — match back to the real
                    // RubricCriteria/RubricLevel rows by name so this ties to actual data.
                    var criteriaEntity = criteriaEntities
                        .FirstOrDefault(c => c.CriteriaName == criterionResult.CriteriaName);

                    var levelEntity = criteriaEntity?.Levels
                        .FirstOrDefault(l => l.LevelName == criterionResult.MatchedTier);

                    context.Add(new AiRubricEvaluation
                    {
                        RubricEvalId = Guid.NewGuid(),
                        EvaluationId = evaluation.EvaluationId,
                        CriteriaId = criteriaEntity?.CriteriaId,
                        LevelId = levelEntity?.LevelId,
                        AssignedScore = criterionResult.AwardedScore,
                        Feedback = criterionResult.Justification,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        // ── Seed a Gemini AiModel row once, reuse it after (idempotent) ──
        private static async Task<Guid> EnsureGeminiModelAsync(AppDbContext context)
        {
            var existing = await context.Set<AiModel>()
                .FirstOrDefaultAsync(m => m.ModelName == GeminiModelName);

            if (existing != null) return existing.ModelId;

            var model = new AiModel
            {
                ModelId = Guid.NewGuid(),
                ModelName = GeminiModelName,
                Provider = "Google",
                ModelVersion = GeminiModelName,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            context.Add(model);
            await context.SaveChangesAsync();
            return model.ModelId;
        }
    }
}
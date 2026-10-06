using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class GradingService
    {
        // ── Left panel: sections + queue of attempts needing (or done with) manual/AI grading ──
        // NOTE: the schema has no "Section" field on Student — grouping uses
        // Student.Program as the closest equivalent. Swap the GroupBy key below
        // if you add a real section/class column later.
        public async Task<GradingDataDto> GetGradingDataAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var subjectiveQuestions = await context.Questions
                .Where(q => q.ExamId == examId && (q.QuestionType == "essay" || q.QuestionType == "coding"))
                .ToListAsync();

            var subjectiveQuestionIds = subjectiveQuestions.Select(q => q.QuestionId).ToHashSet();

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Student)
                .Include(a => a.Answers)
                .Where(a => a.ExamId == examId && (a.Status == "submitted" || a.Status == "graded"))
                .ToListAsync();

            var result = new GradingDataDto();

            foreach (var group in attempts
                         .OrderBy(a => a.Student?.LastName)
                         .GroupBy(a => a.Student?.Program ?? "Unassigned"))
            {
                var section = new GradingSectionDto { SectionName = group.Key };

                foreach (var attempt in group)
                {
                    var subjectiveAnswers = attempt.Answers
                        .Where(ans => ans.QuestionId.HasValue && subjectiveQuestionIds.Contains(ans.QuestionId.Value))
                        .ToList();

                    bool fullyGraded = subjectiveQuestions.Count > 0 &&
                        subjectiveQuestions.All(q => subjectiveAnswers
                            .Any(a => a.QuestionId == q.QuestionId && a.ObtainedPoints.HasValue));

                    section.Attempts.Add(new GradingAttemptDto
                    {
                        AttemptId = attempt.AttemptId,
                        StudentName = attempt.Student != null
                            ? $"{attempt.Student.FirstName} {attempt.Student.LastName}"
                            : "Unknown Student",
                        StudentInitials = attempt.Student != null &&
                                           attempt.Student.FirstName.Length > 0 &&
                                           attempt.Student.LastName.Length > 0
                            ? $"{attempt.Student.FirstName[0]}{attempt.Student.LastName[0]}".ToUpper()
                            : "??",
                        PcLabel = !string.IsNullOrWhiteSpace(attempt.DeviceInfo) ? attempt.DeviceInfo : (attempt.IpAddress ?? attempt.StudentId ?? "N/A"),
                        SectionName = group.Key,
                        IsFullyGraded = fullyGraded
                    });
                }

                result.Sections.Add(section);
            }

            var allAttempts = result.Sections.SelectMany(s => s.Attempts).ToList();

            result.TotalSubmissions = allAttempts.Count;
            result.Finalized = allAttempts.Count(a => a.IsFullyGraded);
            result.PendingReview = result.TotalSubmissions - result.Finalized;
            result.AiAutoGraded = attempts.Count(a => a.Answers.Any(ans =>
                ans.QuestionId.HasValue &&
                subjectiveQuestionIds.Contains(ans.QuestionId.Value) &&
                ans.GradedBy == "ai"));

            return result;
        }

        // ── Right panel: every subjective question + the student's answer, for one attempt ──
        public async Task<List<GradingQuestionDto>> GetQuestionsForAttemptAsync(Guid examId, Guid attemptId)
        {
            using var context = new AppDbContext();

            var questions = await context.Questions
                .Include(q => q.Options)
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();

            var answers = await context.StudentAnswers
                .Where(a => a.AttemptId == attemptId)
                .ToListAsync();

            var answerIds = answers.Select(a => a.AnswerId).ToList();

            // Latest AiEvaluation per answer (an answer could in principle have
            // more than one if it was ever re-graded — take the most recent).
            var evaluations = await context.Set<AiEvaluation>()
                .Where(ev => ev.AnswerId.HasValue && answerIds.Contains(ev.AnswerId.Value))
                .OrderByDescending(ev => ev.EvaluatedAt)
                .ToListAsync();

            var evaluationIds = evaluations.Select(ev => ev.EvaluationId).ToList();

            var rubricEvaluations = await context.Set<AiRubricEvaluation>()
                .Include(re => re.Criteria)
                .Include(re => re.Level)
                .Where(re => re.EvaluationId.HasValue && evaluationIds.Contains(re.EvaluationId.Value))
                .ToListAsync();

            // Most recent failed AiJob per answer, so we can show "AI grading
            // failed" instead of the question just silently having no score.
            var failedJobs = await context.Set<AiJob>()
                .Where(j => j.AnswerId.HasValue && answerIds.Contains(j.AnswerId.Value) && j.Status == "failed")
                .OrderByDescending(j => j.CompletedAt)
                .ToListAsync();

            var result = new List<GradingQuestionDto>();
            int number = 1;

            foreach (var q in questions)
            {
                var ans = answers.FirstOrDefault(a => a.QuestionId == q.QuestionId);

                var evaluation = ans != null
                    ? evaluations.FirstOrDefault(ev => ev.AnswerId == ans.AnswerId)
                    : null;

                var criteriaBreakdown = evaluation != null
                    ? rubricEvaluations
                        .Where(re => re.EvaluationId == evaluation.EvaluationId)
                        .Select(re => new AiCriterionBreakdownDto
                        {
                            CriteriaName = re.Criteria?.CriteriaName ?? "Unknown criterion",
                            MatchedTier = re.Level?.LevelName ?? "Unknown tier",
                            AwardedScore = re.AssignedScore ?? 0,
                            Justification = re.Feedback ?? string.Empty
                        }).ToList()
                    : new List<AiCriterionBreakdownDto>();

                var failedJob = ans != null
                    ? failedJobs.FirstOrDefault(j => j.AnswerId == ans.AnswerId)
                    : null;

                string qType = q.QuestionType switch
                {
                    "multiple_choice" => "Multiple Choice",
                    "true_false" => "True/False",
                    "identification" => "Identification",
                    "enumeration" => "Enumeration",
                    "coding" => $"Coding ({q.ProgrammingLanguage ?? "N/A"})",
                    "essay" => "Essay",
                    _ => q.QuestionType
                };

                string studentAnswer = q.QuestionType == "coding"
                    ? (ans?.SourceCode ?? (ans?.AnswerText ?? "(no submission)"))
                    : (ans?.AnswerText ?? "(no submission)");

                string expectedAnswer = q.CorrectAnswer ?? string.Empty;
                if (q.QuestionType == "multiple_choice" && !string.IsNullOrEmpty(q.CorrectAnswer))
                {
                    expectedAnswer = AutoCheckerService.FormatOptionDisplay(q.CorrectAnswer, q.Options);
                }

                bool isObjective = q.QuestionType != "essay" && q.QuestionType != "coding";
                string matchStatus = string.Empty;
                if (isObjective)
                {
                    if (ans?.ObtainedPoints.HasValue == true && ans.ObtainedPoints.Value > 0 && ans.ObtainedPoints.Value == (q.Points ?? 0))
                    {
                        matchStatus = "Correct";
                    }
                    else if (AutoCheckerService.IsAnswerCorrect(ans?.AnswerText, q))
                    {
                        matchStatus = "Correct";
                    }
                    else
                    {
                        matchStatus = DetermineMatchStatus(ans?.AnswerText, q.CorrectAnswer, qType);
                    }
                }

                result.Add(new GradingQuestionDto
                {
                    AnswerId = ans?.AnswerId ?? Guid.Empty,
                    QuestionId = q.QuestionId,
                    QuestionNumber = number++,
                    QuestionText = q.Text,
                    QuestionType = qType,
                    StudentAnswerText = studentAnswer,
                    ExpectedAnswer = expectedAnswer,
                    MatchStatus = matchStatus,
                    MaxPoints = q.Points ?? 0,
                    ObtainedPoints = ans?.ObtainedPoints,
                    GradedBy = ans?.GradedBy,
                    AiOverallFeedback = evaluation?.Feedback,
                    AiCriteriaBreakdown = criteriaBreakdown,
                    AiGradingFailed = failedJob != null,
                    AiGradingError = failedJob?.LastError
                });
            }

            return result;
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static string DetermineMatchStatus(string? studentText, string? expectedText, string questionType)
        {
            if (string.IsNullOrWhiteSpace(studentText) || studentText.Trim().Equals("(no submission)", StringComparison.OrdinalIgnoreCase))
                return "Incorrect";
            if (string.IsNullOrWhiteSpace(expectedText))
                return "Incorrect";

            var s = studentText.Trim();
            var e = expectedText.Trim();

            if (string.Equals(s, e, StringComparison.OrdinalIgnoreCase))
                return "Correct";

            var eParts = e.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
            if (eParts.Count > 1)
            {
                var sParts = s.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
                int matched = sParts.Count(sp => eParts.Any(ep => string.Equals(sp, ep, StringComparison.OrdinalIgnoreCase) || ep.IndexOf(sp, StringComparison.OrdinalIgnoreCase) >= 0));
                if (matched >= eParts.Count) return "Correct";
                if (matched > 0) return "Fuzzy / Near Match";
            }

            if (s.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0 || e.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                return "Fuzzy / Near Match";

            if (ComputeLevenshteinDistance(s.ToLowerInvariant(), e.ToLowerInvariant()) <= 2)
                return "Fuzzy / Near Match";

            return "Incorrect";
        }

        // ── Persist grades for one attempt, then recompute the final score if fully graded ──
        public async Task SaveGradesAsync(Guid attemptId, List<GradingQuestionDto> questions)
        {
            using var context = new AppDbContext();

            foreach (var q in questions)
            {
                StudentAnswer? answer = null;
                if (q.AnswerId != Guid.Empty)
                {
                    answer = await context.StudentAnswers.FindAsync(q.AnswerId);
                }

                if (answer == null && q.QuestionId != Guid.Empty && q.ObtainedPoints.HasValue)
                {
                    answer = new StudentAnswer
                    {
                        AnswerId = Guid.NewGuid(),
                        AttemptId = attemptId,
                        QuestionId = q.QuestionId,
                        AnswerText = string.Empty
                    };
                    context.StudentAnswers.Add(answer);
                }

                if (answer != null)
                {
                    answer.ObtainedPoints = q.ObtainedPoints;
                    answer.GradedBy = "instructor";
                    answer.GradedAt = DateTime.Now;
                    answer.IsManualGrade = true;
                }
            }

            await context.SaveChangesAsync();

            var attempt = await context.StudentExamAttempts
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

            if (attempt != null && attempt.Answers.Count > 0 && attempt.Answers.All(a => a.ObtainedPoints.HasValue))
            {
                // All questions (objective + subjective) now have a score, so
                // we can compute a running total — but we do NOT flip Status
                // to "graded" here. That's a separate, explicit "Finalize
                // Grades" action (ExamStatusService.FinalizeExamGradesAsync),
                // so instructors get a distinct review-complete vs.
                // finalized-and-locked state.
                attempt.TotalScore = attempt.Answers.Sum(a => a.ObtainedPoints ?? 0);
                await context.SaveChangesAsync();
            }
        }
    }

    public class GradingDataDto
    {
        public List<GradingSectionDto> Sections { get; set; } = new();
        public int TotalSubmissions { get; set; }
        public int AiAutoGraded { get; set; }
        public int PendingReview { get; set; }
        public int Finalized { get; set; }
    }

    public class GradingSectionDto
    {
        public string SectionName { get; set; } = string.Empty;
        public List<GradingAttemptDto> Attempts { get; set; } = new();
    }

    public class GradingAttemptDto
    {
        public Guid AttemptId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentInitials { get; set; } = string.Empty;
        public string PcLabel { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public bool IsFullyGraded { get; set; }
    }

    public class GradingQuestionDto
    {
        public Guid AnswerId { get; set; }
        public Guid QuestionId { get; set; }
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string StudentAnswerText { get; set; } = string.Empty;
        public string? ExpectedAnswer { get; set; }
        public string? MatchStatus { get; set; }
        public decimal MaxPoints { get; set; }
        public decimal? ObtainedPoints { get; set; }

        /// <summary>"ai" | "instructor" | null (never graded). Drives the
        /// "AI-Suggested" vs "Instructor Confirmed" badge in GradingTab.</summary>
        public string? GradedBy { get; set; }

        /// <summary>Null if the AI hasn't graded this yet (or grading failed).</summary>
        public string? AiOverallFeedback { get; set; }

        /// <summary>Empty if no AI evaluation exists yet for this answer.</summary>
        public List<AiCriterionBreakdownDto> AiCriteriaBreakdown { get; set; } = new();

        /// <summary>True if AI grading was attempted and failed (exhausted retries) —
        /// distinguishes "AI hasn't gotten to this yet" from "AI tried and couldn't."</summary>
        public bool AiGradingFailed { get; set; }
        public string? AiGradingError { get; set; }
    }

    public class AiCriterionBreakdownDto
    {
        public string CriteriaName { get; set; } = string.Empty;
        public string MatchedTier { get; set; } = string.Empty;
        public decimal AwardedScore { get; set; }
        public string Justification { get; set; } = string.Empty;
    }
}
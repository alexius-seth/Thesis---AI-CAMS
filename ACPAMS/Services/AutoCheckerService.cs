using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    /// <summary>
    /// Grades objective question types — multiple_choice, true_false,
    /// identification, enumeration — by exact comparison against
    /// Question.CorrectAnswer. These have exactly one right answer sitting
    /// in the database already, so there's no need for AI judgment; this
    /// runs instantly and deterministically, unlike essay/coding grading
    /// which goes through the AiJob queue.
    ///
    /// Runs at two points, so it's never accidentally skipped:
    ///   1. Immediately when a student submits (ExamService.SubmitExamAsync)
    ///   2. Defensively, whenever anyone views ExamScoresPage for an exam —
    ///      catches anything that slipped through for any reason.
    /// </summary>
    public class AutoCheckerService
    {
        private static readonly HashSet<string> ObjectiveTypes = new()
        {
            "multiple_choice", "true_false", "identification", "enumeration"
        };

        // ── Called immediately on submit — grades just this one attempt ──
        public async Task AutoGradeAttemptAsync(Guid attemptId)
        {
            using var context = new AppDbContext();

            var answers = await context.StudentAnswers
                .Include(a => a.Question)
                .Where(a => a.AttemptId == attemptId
                         && a.Question != null
                         && ObjectiveTypes.Contains(a.Question.QuestionType)
                         && !a.ObtainedPoints.HasValue) // don't clobber an existing/manual grade
                .ToListAsync();

            if (answers.Count == 0) return;

            foreach (var answer in answers)
            {
                GradeOneAnswer(answer, answer.Question!);
            }

            await context.SaveChangesAsync();

            // BUG FIX: this was missing entirely — individual StudentAnswer
            // rows got scored, but StudentExamAttempt.TotalScore (a cached
            // column ReportsService reads directly) never got refreshed.
            // ExamScoresPage looked correct because it sums Answers fresh
            // every time; ReportsTab looked stale because it trusts this
            // cached value, which nothing was updating.
            await RecomputeAttemptTotalAsync(context, attemptId);
        }

        // ── Defensive sweep — grades every ungraded objective answer across
        // every attempt for an exam. Safe to call repeatedly; only touches
        // answers that don't already have a score. ──
        public async Task AutoGradeExamAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var answers = await context.StudentAnswers
                .Include(a => a.Question)
                .Include(a => a.Attempt)
                .Where(a => a.Question != null
                         && a.Question.ExamId == examId
                         && ObjectiveTypes.Contains(a.Question.QuestionType)
                         && !a.ObtainedPoints.HasValue
                         && a.Attempt != null
                         && (a.Attempt.Status == "submitted" || a.Attempt.Status == "graded"))
                .ToListAsync();

            if (answers.Count == 0) return;

            var affectedAttemptIds = answers
                .Where(a => a.AttemptId.HasValue)
                .Select(a => a.AttemptId!.Value)
                .Distinct()
                .ToList();

            foreach (var answer in answers)
            {
                GradeOneAnswer(answer, answer.Question!);
            }

            await context.SaveChangesAsync();

            // Same fix as AutoGradeAttemptAsync — refresh every affected
            // attempt's cached TotalScore, not just the individual answers.
            foreach (var attemptId in affectedAttemptIds)
            {
                await RecomputeAttemptTotalAsync(context, attemptId);
            }
        }

        // ── Shared: recompute TotalScore once every answer has a score ──
        private static async Task RecomputeAttemptTotalAsync(AppDbContext context, Guid attemptId)
        {
            var attempt = await context.StudentExamAttempts
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

            if (attempt != null && attempt.Answers.Count > 0 && attempt.Answers.All(a => a.ObtainedPoints.HasValue))
            {
                attempt.TotalScore = attempt.Answers.Sum(a => a.ObtainedPoints ?? 0);
                await context.SaveChangesAsync();
            }
        }

        private static void GradeOneAnswer(StudentAnswer answer, Question question)
        {
            bool isCorrect = string.Equals(
                (answer.AnswerText ?? string.Empty).Trim(),
                (question.CorrectAnswer ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);

            answer.ObtainedPoints = isCorrect ? (question.Points ?? 0) : 0;
            answer.GradedBy = "system";
            answer.GradedAt = DateTime.Now;
            answer.IsManualGrade = false;
        }
    }
}

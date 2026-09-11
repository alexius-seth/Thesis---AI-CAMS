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
                    .ThenInclude(q => q!.Options)
                .Where(a => a.AttemptId == attemptId
                         && a.Question != null
                         && ObjectiveTypes.Contains(a.Question.QuestionType)
                         && (!a.ObtainedPoints.HasValue || (a.GradedBy == "system" && !a.IsManualGrade)))
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
        // every attempt for an exam. Safe to call repeatedly; also re-checks
        // un-overridden system grades to rectify past mismatches. ──
        public async Task AutoGradeExamAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var answers = await context.StudentAnswers
                .Include(a => a.Question)
                    .ThenInclude(q => q!.Options)
                .Include(a => a.Attempt)
                .Where(a => a.Question != null
                         && a.Question.ExamId == examId
                         && ObjectiveTypes.Contains(a.Question.QuestionType)
                         && (!a.ObtainedPoints.HasValue || (a.GradedBy == "system" && !a.IsManualGrade))
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
            bool isCorrect = IsAnswerCorrect(answer.AnswerText, question);

            answer.ObtainedPoints = isCorrect ? (question.Points ?? 0) : 0;
            answer.GradedBy = "system";
            answer.GradedAt = DateTime.Now;
            answer.IsManualGrade = false;
        }

        public static bool IsAnswerCorrect(string? studentAnswerText, Question question)
        {
            var sTrim = (studentAnswerText ?? string.Empty).Trim();
            var cTrim = (question.CorrectAnswer ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(sTrim) || string.IsNullOrEmpty(cTrim))
                return false;

            if (string.Equals(sTrim, cTrim, StringComparison.OrdinalIgnoreCase))
                return true;

            if (question.QuestionType == "multiple_choice")
            {
                var (sLetter, sText) = ResolveOption(sTrim, question.Options);
                var (cLetter, cText) = ResolveOption(cTrim, question.Options);

                if (!string.IsNullOrEmpty(sLetter) && !string.IsNullOrEmpty(cLetter)
                    && string.Equals(sLetter, cLetter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(sText) && !string.IsNullOrEmpty(cText)
                    && string.Equals(sText, cText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static (string? Letter, string? Text) ResolveOption(string? raw, IEnumerable<QuestionOption>? options)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return (null, null);

            var trimmed = raw.Trim();
            var ordered = (options ?? Enumerable.Empty<QuestionOption>())
                .OrderBy(o => o.OrderIndex)
                .Select((opt, idx) => new
                {
                    Letter = ((char)('A' + idx)).ToString(),
                    Text = (opt.OptionText ?? string.Empty).Trim()
                })
                .ToList();

            // 1. Exact match by option text
            var textMatch = ordered.FirstOrDefault(o =>
                !string.IsNullOrEmpty(o.Text) &&
                string.Equals(o.Text, trimmed, StringComparison.OrdinalIgnoreCase));
            if (textMatch != null)
                return (textMatch.Letter, textMatch.Text);

            // 2. Letter prefix or exact letter key (e.g. "A", "A.", "A)", "(A)", "A: text")
            var letterMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\(?([A-Za-z])(?:\.|\)|\:|\s|$)");
            if (letterMatch.Success)
            {
                string letter = letterMatch.Groups[1].Value.ToUpperInvariant();
                var match = ordered.FirstOrDefault(o => string.Equals(o.Letter, letter, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return (match.Letter, match.Text);

                if (ordered.Count == 0)
                    return (letter, null);
            }

            // 3. Fallback: contains match (e.g. if raw had extra surrounding text)
            var containsMatch = ordered.FirstOrDefault(o =>
                !string.IsNullOrEmpty(o.Text) &&
                (trimmed.IndexOf(o.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 o.Text.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0));
            if (containsMatch != null)
                return (containsMatch.Letter, containsMatch.Text);

            return (null, trimmed);
        }

        public static string FormatOptionDisplay(string? raw, IEnumerable<QuestionOption>? options)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? string.Empty;
            var (letter, text) = ResolveOption(raw, options);
            if (!string.IsNullOrEmpty(letter) && !string.IsNullOrEmpty(text))
                return $"{letter}. {text}";
            if (!string.IsNullOrEmpty(letter))
                return letter;
            if (!string.IsNullOrEmpty(text))
                return text;
            return raw.Trim();
        }
    }
}

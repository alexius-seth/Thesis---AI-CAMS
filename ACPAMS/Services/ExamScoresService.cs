using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    /// <summary>
    /// Powers ExamScoresPage: given one exam, returns a class-wide summary
    /// (test takers, average, how many aren't finalized yet) plus a
    /// per-student row with scores broken down by section, and a running
    /// total on the right — matching how the exam was actually built with
    /// SectionName groupings in ExamService.SaveExamAsync.
    /// </summary>
    public class ExamScoresService
    {
        public async Task<ExamScoresSummaryDto> GetExamScoresAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var exam = await context.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);

            var questions = await context.Questions
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();

            // Section order = first appearance order among questions (OrderIndex),
            // not alphabetical — matches how the exam was actually laid out.
            var sectionNames = questions
                .Select(q => q.SectionName ?? "General")
                .Distinct()
                .ToList();

            var sectionMaxPoints = sectionNames.ToDictionary(
                name => name,
                name => questions.Where(q => (q.SectionName ?? "General") == name).Sum(q => q.Points ?? 0));

            decimal examMaxPoints = questions.Sum(q => q.Points ?? 0);

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Student)
                .Include(a => a.Answers)
                .Where(a => a.ExamId == examId && (a.Status == "submitted" || a.Status == "graded"))
                .ToListAsync();

            var result = new ExamScoresSummaryDto
            {
                ExamTitle = exam?.Title ?? exam?.ExamName ?? "Untitled Exam",
                SectionNames = sectionNames,
                TotalTestTakers = attempts.Count
            };

            var completedTotals = new List<decimal>();

            foreach (var attempt in attempts)
            {
                var row = new StudentExamScoreRowDto
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
                    IsFinalized = attempt.Status == "graded",
                    SectionMaxPoints = sectionMaxPoints
                };

                bool anyUnscored = false;

                foreach (var sectionName in sectionNames)
                {
                    var sectionQuestionIds = questions
                        .Where(q => (q.SectionName ?? "General") == sectionName)
                        .Select(q => q.QuestionId)
                        .ToHashSet();

                    var sectionAnswers = attempt.Answers
                        .Where(a => a.QuestionId.HasValue && sectionQuestionIds.Contains(a.QuestionId.Value))
                        .ToList();

                    bool sectionFullyScored = sectionAnswers.Count > 0 &&
                        sectionAnswers.All(a => a.ObtainedPoints.HasValue);

                    if (sectionAnswers.Any(a => !a.ObtainedPoints.HasValue))
                        anyUnscored = true;

                    row.SectionScores[sectionName] = sectionFullyScored
                        ? sectionAnswers.Sum(a => a.ObtainedPoints ?? 0)
                        : (decimal?)null;

                    decimal sectionMax = sectionMaxPoints.TryGetValue(sectionName, out var m) ? m : 0;
                    row.SectionScoreTexts[sectionName] = row.SectionScores[sectionName].HasValue
                        ? $"{row.SectionScores[sectionName].Value:0.##} / {sectionMax:0.##}"
                        : "Pending";
                }

                row.HasUnscoredAnswers = anyUnscored;
                row.TotalMaxPoints = examMaxPoints;

                row.TotalScore = !anyUnscored && attempt.Answers.Count > 0
                    ? attempt.Answers.Sum(a => a.ObtainedPoints ?? 0)
                    : (decimal?)null;

                row.TotalScoreText = row.TotalScore.HasValue
                    ? $"{row.TotalScore.Value:0.##} / {examMaxPoints:0.##}"
                    : "Pending";

                if (row.TotalScore.HasValue)
                    completedTotals.Add(row.TotalScore.Value);

                result.Rows.Add(row);
            }

            result.UnfinalizedCount = attempts.Count(a => a.Status != "graded");

            if (completedTotals.Count > 0 && examMaxPoints > 0)
            {
                result.HasAverage = true;
                result.AverageScorePercent = Math.Round(
                    completedTotals.Average(t => (t / examMaxPoints) * 100m), 1);
            }

            return result;
        }

        // ── Full per-question answer breakdown for one student's attempt ──
        // Used by ExamScoresPage's click-to-expand detail panel.
        public async Task<List<StudentAnswerDetailDto>> GetStudentAnswerDetailsAsync(Guid attemptId)
        {
            using var context = new AppDbContext();

            var attempt = await context.StudentExamAttempts
                .Include(a => a.Answers)
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

            if (attempt?.Exam == null) return new List<StudentAnswerDetailDto>();

            var questions = await context.Questions
                .Include(q => q.Options)
                .Where(q => q.ExamId == attempt.Exam.ExamId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();

            var objectiveTypes = new HashSet<string> { "multiple_choice", "true_false", "identification", "enumeration" };

            var result = new List<StudentAnswerDetailDto>();
            int number = 1;

            foreach (var q in questions)
            {
                var ans = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.QuestionId);
                bool isObjective = objectiveTypes.Contains(q.QuestionType);

                string studentAnswerText;
                if (q.QuestionType == "coding")
                {
                    studentAnswerText = ans?.SourceCode ?? "(no submission)";
                }
                else if (string.IsNullOrWhiteSpace(ans?.AnswerText))
                {
                    studentAnswerText = "(no submission)";
                }
                else if (q.QuestionType == "multiple_choice")
                {
                    studentAnswerText = AutoCheckerService.FormatOptionDisplay(ans.AnswerText, q.Options);
                }
                else
                {
                    studentAnswerText = ans.AnswerText;
                }

                string? correctAnswerText = null;
                if (isObjective)
                {
                    correctAnswerText = q.QuestionType == "multiple_choice"
                        ? AutoCheckerService.FormatOptionDisplay(q.CorrectAnswer, q.Options)
                        : q.CorrectAnswer;
                }

                result.Add(new StudentAnswerDetailDto
                {
                    QuestionNumber = number++,
                    QuestionText = q.Text,
                    QuestionType = q.QuestionType,
                    StudentAnswerText = studentAnswerText,
                    CorrectAnswerText = correctAnswerText,
                    ObtainedPoints = ans?.ObtainedPoints,
                    MaxPoints = q.Points ?? 0,
                    IsObjective = isObjective,
                    GradedBy = ans?.GradedBy
                });
            }

            return result;
        }
    }

    public class ExamScoresSummaryDto
    {
        public string ExamTitle { get; set; } = string.Empty;
        public int TotalTestTakers { get; set; }
        public bool HasAverage { get; set; }
        public decimal AverageScorePercent { get; set; }
        public int UnfinalizedCount { get; set; }
        public List<string> SectionNames { get; set; } = new();
        public List<StudentExamScoreRowDto> Rows { get; set; } = new();
    }

    public class StudentExamScoreRowDto
    {
        public Guid AttemptId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentInitials { get; set; } = string.Empty;

        /// <summary>Section name -> obtained points for that section. Null means
        /// that section isn't fully scored yet (still pending AI/manual grading).</summary>
        public Dictionary<string, decimal?> SectionScores { get; set; } = new();
        public Dictionary<string, decimal> SectionMaxPoints { get; set; } = new();

        /// <summary>Pre-formatted "12.00 / 20.00" or "Pending" per section, keyed by
        /// section name — bind directly via SectionScoreTexts[sectionName] in XAML,
        /// no converter needed.</summary>
        public Dictionary<string, string> SectionScoreTexts { get; set; } = new();

        /// <summary>Null until every question across every section has a score.</summary>
        public decimal? TotalScore { get; set; }
        public decimal TotalMaxPoints { get; set; }
        public string TotalScoreText { get; set; } = "Pending";

        /// <summary>True once the instructor has clicked Finalize Grades for this exam
        /// (attempt.Status == "graded") — not just "every question has a number."</summary>
        public bool IsFinalized { get; set; }

        /// <summary>True if any answer (in any section) has no score yet — the
        /// signal for showing a "Review in Grading" link back to GradingTab.</summary>
        public bool HasUnscoredAnswers { get; set; }
    }

    public class StudentAnswerDetailDto
    {
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public string StudentAnswerText { get; set; } = string.Empty;

        /// <summary>Null for essay/coding questions — no single correct answer exists.</summary>
        public string? CorrectAnswerText { get; set; }
        public decimal? ObtainedPoints { get; set; }
        public decimal MaxPoints { get; set; }
        public bool IsObjective { get; set; }
        public string? GradedBy { get; set; }
    }
}

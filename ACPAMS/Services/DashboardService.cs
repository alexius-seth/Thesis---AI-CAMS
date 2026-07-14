using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class DashboardService
    {
        // ── Load a specific exam directly (e.g. "View Dashboard" from Faculty Home) ──
        public async Task<LiveExamDto?> GetExamByIdAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var exam = await context.Exams
                .Include(e => e.Attempts)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            if (exam == null) return null;

            var attempts = exam.Attempts ?? new List<StudentExamAttempt>();

            return new LiveExamDto
            {
                ExamId = exam.ExamId,
                Title = exam.Title ?? exam.ExamName ?? "Untitled Exam",
                CourseCode = exam.CourseCode ?? "N/A",
                StudentsTaking = attempts.Count(a => a.Status == "in_progress"),
                StudentsFinished = attempts.Count(a => a.Status == "submitted" || a.Status == "graded"),
                TotalAttempts = attempts.Count
            };
        }

        // ── Find the exam currently being taken by this instructor ──────
        public async Task<LiveExamDto?> GetLiveExamAsync(string instructorId)
        {
            using var context = new AppDbContext();

            var exam = await context.Exams
                .Include(e => e.Attempts)
                .Where(e => e.InstructorId == instructorId)
                .Where(e => e.Attempts.Any(a => a.Status == "in_progress"))
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (exam == null) return null;

            var attempts = exam.Attempts ?? new List<StudentExamAttempt>();

            return new LiveExamDto
            {
                ExamId = exam.ExamId,
                Title = exam.Title ?? exam.ExamName ?? "Untitled Exam",
                CourseCode = exam.CourseCode ?? "N/A",
                StudentsTaking = attempts.Count(a => a.Status == "in_progress"),
                StudentsFinished = attempts.Count(a => a.Status == "submitted" || a.Status == "graded"),
                TotalAttempts = attempts.Count
            };
        }

        // ── Assessment Completion Status table rows ──────────────────────
        public async Task<List<StudentRowDto>> GetStudentRowsAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Student)
                .Where(a => a.ExamId == examId)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync();

            var rows = new List<StudentRowDto>();

            foreach (var a in attempts)
            {
                var flagCount = await context.AuditLogs.CountAsync(l =>
                    l.EntityType == "StudentExamAttempt" &&
                    l.EntityId == a.AttemptId.ToString() &&
                    !l.Action.Contains("HeadTurned") &&
                    !l.Action.Contains("HeadTilted"));

                string risk = flagCount >= 5 ? "High" : flagCount >= 2 ? "Medium" : "Low";

                rows.Add(new StudentRowDto
                {
                    StudentName = a.Student != null ? $"{a.Student.FirstName} {a.Student.LastName}" : "Unknown",
                    Initials = a.Student != null && a.Student.FirstName.Length > 0 && a.Student.LastName.Length > 0
                        ? $"{a.Student.FirstName[0]}{a.Student.LastName[0]}".ToUpper()
                        : "??",
                    Program = a.Student?.Program ?? "N/A",
                    Risk = risk,
                    AutoScore = a.TotalScore,
                    Status = a.Status ?? "in_progress"
                });
            }

            return rows;
        }

        // ── Auto-grade objective questions and build score distribution ──
        // Distribution bands are % of THIS EXAM'S maximum possible score
        // (sum of every question's points, objective + subjective combined),
        // not a fixed 100. An attempt only counts once ALL of its questions
        // have a score — for objective-only exams that happens immediately
        // here; for mixed exams it happens once grading is finished via
        // GradingService.SaveGradesAsync / ExamStatusService.FinalizeExamGradesAsync.
        public async Task<ScoreDistributionDto> AutoGradeAndGetDistributionAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var allQuestions = await context.Questions
                .Where(q => q.ExamId == examId)
                .ToListAsync();

            var objectiveQuestions = allQuestions
                .Where(q => q.QuestionType != "essay" && q.QuestionType != "coding")
                .ToList();

            decimal maxExamPoints = allQuestions.Sum(q => q.Points ?? 0);

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Answers)
                .Where(a => a.ExamId == examId && (a.Status == "submitted" || a.Status == "graded"))
                .ToListAsync();

            bool anyChanges = false;

            foreach (var attempt in attempts)
            {
                foreach (var q in objectiveQuestions)
                {
                    var ans = attempt.Answers.FirstOrDefault(x => x.QuestionId == q.QuestionId);
                    if (ans == null || ans.ObtainedPoints.HasValue) continue; // don't clobber an existing/manual grade

                    bool isCorrect = string.Equals(
                        (ans.AnswerText ?? "").Trim(),
                        (q.CorrectAnswer ?? "").Trim(),
                        StringComparison.OrdinalIgnoreCase);

                    ans.ObtainedPoints = isCorrect ? (q.Points ?? 0) : 0;
                    ans.GradedBy = "system";
                    ans.GradedAt = DateTime.Now;
                    ans.IsManualGrade = false;
                    anyChanges = true;
                }

                // Once every question on this attempt has a score (whether just
                // auto-graded above, or previously graded manually/by AI for the
                // subjective ones), compute the attempt's total.
                if (!attempt.TotalScore.HasValue &&
                    attempt.Answers.Count > 0 &&
                    attempt.Answers.All(a => a.ObtainedPoints.HasValue))
                {
                    attempt.TotalScore = attempt.Answers.Sum(a => a.ObtainedPoints ?? 0);
                    anyChanges = true;
                }
            }

            if (anyChanges)
                await context.SaveChangesAsync();

            var dist = new ScoreDistributionDto();

            foreach (var attempt in attempts.Where(a => a.TotalScore.HasValue))
            {
                double pct = maxExamPoints > 0 ? (double)(attempt.TotalScore!.Value / maxExamPoints) * 100 : 0;

                if (pct >= 90) dist.Band90to100++;
                else if (pct >= 80) dist.Band80to89++;
                else if (pct >= 70) dist.Band70to79++;
                else dist.BelowSeventy++;
            }

            return dist;
        }

        // ── Class Pass Rate + Average Score KPIs ──────────────────────
        // Both are computed as a % of the exam's maximum possible score,
        // across every attempt that already has a final TotalScore.
        // Passing threshold is a guess (75%, common in PH grading) —
        // adjust PASSING_PERCENT below if your actual cutoff differs.
        private const decimal PASSING_PERCENT = 75m;

        public async Task<ClassSummaryDto> GetClassSummaryAsync(Guid examId)
        {
            using var context = new AppDbContext();

            decimal maxExamPoints = await context.Questions
                .Where(q => q.ExamId == examId)
                .SumAsync(q => q.Points ?? 0);

            var scores = await context.StudentExamAttempts
                .Where(a => a.ExamId == examId && a.TotalScore.HasValue)
                .Select(a => a.TotalScore!.Value)
                .ToListAsync();

            if (scores.Count == 0 || maxExamPoints <= 0)
            {
                return new ClassSummaryDto { HasData = false };
            }

            decimal passingScore = maxExamPoints * PASSING_PERCENT / 100m;
            int passCount = scores.Count(s => s >= passingScore);

            decimal avgPercent = scores.Average(s => (s / maxExamPoints) * 100m);
            decimal passRatePercent = (decimal)passCount / scores.Count * 100m;

            return new ClassSummaryDto
            {
                HasData = true,
                PassRatePercent = Math.Round(passRatePercent),
                AverageScorePercent = Math.Round(avgPercent),
                GradedCount = scores.Count
            };
        }
    }

    public class LiveExamDto
    {
        public Guid ExamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public int StudentsTaking { get; set; }
        public int StudentsFinished { get; set; }
        public int TotalAttempts { get; set; }
    }

    public class StudentRowDto
    {
        public string StudentName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string Risk { get; set; } = "Low";
        public decimal? AutoScore { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ScoreDistributionDto
    {
        public int Band90to100 { get; set; }
        public int Band80to89 { get; set; }
        public int Band70to79 { get; set; }
        public int BelowSeventy { get; set; }
        public int Max => Math.Max(1, new[] { Band90to100, Band80to89, Band70to79, BelowSeventy }.Max());
    }

    public class ClassSummaryDto
    {
        public bool HasData { get; set; }
        public decimal PassRatePercent { get; set; }
        public decimal AverageScorePercent { get; set; }
        public int GradedCount { get; set; }
    }
}
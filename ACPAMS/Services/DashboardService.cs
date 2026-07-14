using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class DashboardService
    {
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
        // NOTE: TotalScore is only auto-set for exams with ZERO essay/coding
        // questions, since a mixed exam's real total isn't known until the
        // subjective parts are graded (manually or by AI). Score Distribution
        // for mixed exams will be empty until that grading exists.
        public async Task<ScoreDistributionDto> AutoGradeAndGetDistributionAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var allQuestions = await context.Questions
                .Where(q => q.ExamId == examId)
                .ToListAsync();

            var objectiveQuestions = allQuestions
                .Where(q => q.QuestionType != "essay" && q.QuestionType != "coding")
                .ToList();

            bool hasSubjective = allQuestions.Any(q => q.QuestionType == "essay" || q.QuestionType == "coding");
            decimal maxObjectivePoints = objectiveQuestions.Sum(q => q.Points ?? 0);

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Answers)
                .Where(a => a.ExamId == examId && (a.Status == "submitted" || a.Status == "graded"))
                .ToListAsync();

            foreach (var attempt in attempts)
            {
                decimal objectiveTotal = 0;

                foreach (var q in objectiveQuestions)
                {
                    var ans = attempt.Answers.FirstOrDefault(x => x.QuestionId == q.QuestionId);
                    if (ans == null) continue;

                    bool isCorrect = string.Equals(
                        (ans.AnswerText ?? "").Trim(),
                        (q.CorrectAnswer ?? "").Trim(),
                        StringComparison.OrdinalIgnoreCase);

                    ans.ObtainedPoints = isCorrect ? (q.Points ?? 0) : 0;
                    ans.GradedBy = "system";
                    ans.GradedAt = DateTime.Now;
                    ans.IsManualGrade = false;

                    if (isCorrect) objectiveTotal += q.Points ?? 0;
                }

                if (!hasSubjective)
                    attempt.TotalScore = objectiveTotal;
            }

            await context.SaveChangesAsync();

            var dist = new ScoreDistributionDto();
            decimal denominator = hasSubjective ? 0 : maxObjectivePoints;

            foreach (var attempt in attempts.Where(a => a.TotalScore.HasValue && !hasSubjective))
            {
                double pct = denominator > 0 ? (double)(attempt.TotalScore!.Value / denominator) * 100 : 0;

                if (pct >= 90) dist.Band90to100++;
                else if (pct >= 80) dist.Band80to89++;
                else if (pct >= 70) dist.Band70to79++;
                else dist.BelowSeventy++;
            }

            return dist;
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
}

using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class StudentInformationService
    {
        // Placeholder thresholds — same category of "sensitive, needs
        // institutional/client policy input" as the Pass/Fail status
        private static decimal ExcellentThreshold => AppSettingsService.ExcellentThreshold;
        private static decimal GoodThreshold => AppSettingsService.GoodThreshold;
        private static decimal FairThreshold => AppSettingsService.FairThreshold;

        public async Task<StudentProfileSummaryDto> GetStudentProfileSummaryAsync(string studentId)
        {
            using var context = new AppDbContext();

            var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
            if (student == null)
                throw new InvalidOperationException($"Student {studentId} not found.");

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Exam)
                .Include(a => a.Answers)
                    .ThenInclude(ans => ans.Question)
                .Where(a => a.StudentId == studentId && (a.Status == "submitted" || a.Status == "graded"))
                .OrderBy(a => a.SubmittedAt ?? a.StartedAt)
                .ToListAsync();

            var result = new StudentProfileSummaryDto
            {
                StudentName = $"{student.FirstName} {student.LastName}",
                Program = student.Program ?? "N/A",
                StudentId = student.StudentId,
                // Section isn't a tracked field in the current schema —
                // StudentReportViewModel already hardcodes this same "N/A"
                // for the same reason; kept consistent rather than inventing
                // a property that doesn't exist.
                Section = "N/A"
            };

            var percentPerAttempt = new List<(DateTime Date, decimal Percent)>();

            foreach (var attempt in attempts)
            {
                if (!attempt.TotalScore.HasValue) continue;

                decimal maxPoints = attempt.Answers
                    .Where(a => a.Question != null)
                    .Select(a => a.Question!)
                    .Distinct()
                    .Sum(q => q.Points ?? 0);

                if (maxPoints <= 0) continue;

                decimal percent = attempt.TotalScore.Value / maxPoints * 100m;
                DateTime date = attempt.SubmittedAt ?? attempt.StartedAt ?? DateTime.Now;

                percentPerAttempt.Add((date, percent));

                var types = attempt.Answers
                    .Where(a => a.Question != null)
                    .Select(a => a.Question!.QuestionType)
                    .Distinct()
                    .Select(FormatQuestionType)
                    .OrderBy(t => t)
                    .ToList();

                result.AssessmentRecords.Add(new AssessmentRecordDto
                {
                    ExamName = attempt.Exam?.Title ?? attempt.Exam?.ExamName ?? "Untitled Exam",
                    DateTaken = date,
                    TypesSummary = types.Count > 0 ? string.Join(", ", types) : "—",
                    FinalGradeText = $"{attempt.TotalScore.Value:0.##} / {maxPoints:0.##}"
                    // Status intentionally omitted — see AssessmentRecordDto.
                });
            }

            result.TotalAssessments = percentPerAttempt.Count;

            if (percentPerAttempt.Count > 0)
            {
                result.HasOverallAverage = true;
                result.OverallAveragePercent = Math.Round(percentPerAttempt.Average(p => p.Percent), 1);
                result.AcademicStanding = GetStanding(result.OverallAveragePercent);
            }

            result.PerformanceTrend = percentPerAttempt
                .Select(p => new TrendPointDto { Date = p.Date, Percent = Math.Round(p.Percent, 1) })
                .ToList();

            // Most recent assessments first for the record table.
            result.AssessmentRecords.Reverse();

            return result;
        }

        private static string GetStanding(decimal averagePercent)
        {
            if (averagePercent >= ExcellentThreshold) return "Excellent";
            if (averagePercent >= GoodThreshold) return "Good";
            if (averagePercent >= FairThreshold) return "Fair";
            return "Needs Improvement";
        }

        private static string FormatQuestionType(string type) => type switch
        {
            "multiple_choice" => "MCQ",
            "true_false" => "True/False",
            "identification" => "Identification",
            "enumeration" => "Enumeration",
            "essay" => "Essay",
            "coding" => "Code",
            _ => type
        };
    }

    public class StudentProfileSummaryDto
    {
        public string StudentName { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;

        public int TotalAssessments { get; set; }
        public bool HasOverallAverage { get; set; }
        public decimal OverallAveragePercent { get; set; }

        /// <summary>"Excellent" | "Good" | "Fair" | "Needs Improvement" | null
        /// (null if no graded assessments exist yet).</summary>
        public string? AcademicStanding { get; set; }

        public List<TrendPointDto> PerformanceTrend { get; set; } = new();
        public List<AssessmentRecordDto> AssessmentRecords { get; set; } = new();
    }

    public class TrendPointDto
    {
        public DateTime Date { get; set; }
        public decimal Percent { get; set; }
    }

    public class AssessmentRecordDto
    {
        public string ExamName { get; set; } = string.Empty;
        public DateTime DateTaken { get; set; }
        public string TypesSummary { get; set; } = string.Empty;
        public string FinalGradeText { get; set; } = string.Empty;

        // Deliberately no Status (Passed/Failed) property — per team
        // guidance this needs client/policy input before being implemented.
        // The UI shows a neutral placeholder instead of real logic.
    }
}

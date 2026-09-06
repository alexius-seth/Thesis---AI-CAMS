using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class ReportsService
    {
        // Rough, rule-based estimate — NOT AI-derived. Flags-per-attempt is
        // normalized against a cap (10 flags = 0% integrity) and averaged.
        // Adjust FLAG_CAP if this feels too harsh/lenient in practice.
        private const int FLAG_CAP = 10;

        // ── Aggregate metrics + per-attempt rows across ALL of this instructor's exams ──
        // Used by the generic "View Student Reports" quick-nav (browse everything).
        public async Task<ReportsSummaryDto> GetSummaryAsync(string instructorId)
        {
            using var context = new AppDbContext();

            var examIds = await context.Exams
                .Where(e => e.InstructorId == instructorId)
                .Select(e => e.ExamId)
                .ToListAsync();

            return await BuildSummaryAsync(context, examIds);
        }

        // ── Same aggregate, scoped to ONE exam only ──────────────────────
        // Used by "View Report" on a specific completed exam, so students from
        // other subjects/exams don't leak into the table.
        public async Task<ReportsSummaryDto> GetSummaryForExamAsync(Guid examId)
        {
            using var context = new AppDbContext();
            return await BuildSummaryAsync(context, new List<Guid> { examId });
        }

        private async Task<ReportsSummaryDto> BuildSummaryAsync(AppDbContext context, List<Guid> examIds)
        {
            var result = new ReportsSummaryDto();

            if (examIds.Count == 0)
                return result;

            // Max points per exam, for turning each attempt's TotalScore into a %.
            var examMaxPoints = await context.Questions
                .Where(q => examIds.Contains(q.ExamId))
                .GroupBy(q => q.ExamId)
                .Select(g => new { ExamId = g.Key, Max = g.Sum(q => q.Points ?? 0) })
                .ToDictionaryAsync(x => x.ExamId, x => x.Max);

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Student)
                .Where(a => examIds.Contains(a.ExamId ?? Guid.Empty))
                .OrderByDescending(a => a.SubmittedAt ?? a.StartedAt)
                .ToListAsync();

            int totalAttempts = attempts.Count;
            var finishedAttempts = attempts.Where(a => a.Status == "submitted" || a.Status == "graded").ToList();

            // ── Class Average ──
            var gradedPercents = new List<decimal>();
            foreach (var a in finishedAttempts)
            {
                if (!a.TotalScore.HasValue) continue;
                if (!examMaxPoints.TryGetValue(a.ExamId ?? Guid.Empty, out var maxPts) || maxPts <= 0) continue;

                gradedPercents.Add(a.TotalScore.Value / maxPts * 100m);
            }

            if (gradedPercents.Count > 0)
            {
                result.HasClassAverage = true;
                result.ClassAveragePercent = Math.Round(gradedPercents.Average(), 1);
                result.GradedCount = gradedPercents.Count;
            }

            // ── Completion Rate ──
            if (totalAttempts > 0)
            {
                result.HasCompletionRate = true;
                result.CompletionRatePercent = Math.Round((decimal)finishedAttempts.Count / totalAttempts * 100m);
                result.TotalAttempts = totalAttempts;
            }

            // ── Integrity Index + per-attempt anomaly counts + infraction breakdown ──
            var attemptIds = attempts.Select(a => a.AttemptId.ToString()).ToList();

            var relevantLogs = await context.AuditLogs
                .Where(l => l.EntityType == "StudentExamAttempt" && attemptIds.Contains(l.EntityId))
                .ToListAsync();

            int gazeCount = relevantLogs.Count(l => l.Action.Contains("NoFaceDetected") || l.Action.Contains("MultipleFaces") || l.Action.Contains("GazeDiverted"));
            int windowCount = relevantLogs.Count(l => l.Action.Contains("AltTab") || l.Action.Contains("WindowFocus"));
            int clipboardCount = relevantLogs.Count(l => l.Action.Contains("Clipboard"));

            result.GazeDeviationCount = gazeCount;
            result.WindowSwitchCount = windowCount;
            result.ClipboardCount = clipboardCount;
            result.TotalInfractions = gazeCount + windowCount + clipboardCount;

            if (finishedAttempts.Count > 0)
            {
                var flagsPerAttempt = finishedAttempts
                    .Select(a => relevantLogs.Count(l => l.EntityId == a.AttemptId.ToString()))
                    .ToList();

                double avgFlags = flagsPerAttempt.Average();
                double integrity = 100.0 * (1.0 - Math.Min(1.0, avgFlags / FLAG_CAP));

                result.HasIntegrityIndex = true;
                result.IntegrityIndexPercent = Math.Round((decimal)integrity);
            }

            // ── Per-attempt rows for the Detailed Performance Data table ──
            foreach (var a in finishedAttempts)
            {
                examMaxPoints.TryGetValue(a.ExamId ?? Guid.Empty, out var maxPts);
                int flagCount = relevantLogs.Count(l => l.EntityId == a.AttemptId.ToString());
                string risk = flagCount >= 5 ? "High" : flagCount >= 2 ? "Medium" : "Low";

                result.Rows.Add(new ReportRowDto
                {
                    AttemptId = a.AttemptId,
                    StudentId = a.StudentId ?? string.Empty,
                    StudentName = a.Student != null ? $"{a.Student.FirstName} {a.Student.LastName}" : "Unknown Student",
                    Initials = a.Student != null && a.Student.FirstName.Length > 0 && a.Student.LastName.Length > 0
                        ? $"{a.Student.FirstName[0]}{a.Student.LastName[0]}".ToUpper()
                        : "??",
                    FinalScoreText = a.TotalScore.HasValue && maxPts > 0
                        ? $"{a.TotalScore.Value:0.##} / {maxPts:0.##}"
                        : "—",
                    IsPassing = a.TotalScore.HasValue && maxPts > 0 && (a.TotalScore.Value / maxPts) >= 0.75m,
                    AnomalyCount = flagCount,
                    RiskLevel = risk
                });
            }

            return result;
        }

        // ── Class-wide average score, per SECTION, for ONE exam ──────────
        // Replaces the old "Class Mastery Profile" radar (which needed
        // per-question skill tagging that doesn't exist) with something
        // genuinely real: the same section grouping ExamScoresPage already
        // shows, just averaged across every student instead of per-student.
        public async Task<List<SectionAverageDto>> GetSectionAveragesForExamAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var questions = await context.Questions
                .Where(q => q.ExamId == examId)
                .ToListAsync();

            var sectionNames = questions
                .Select(q => q.SectionName ?? "General")
                .Distinct()
                .ToList();

            var attempts = await context.StudentExamAttempts
                .Include(a => a.Answers)
                .Where(a => a.ExamId == examId && (a.Status == "submitted" || a.Status == "graded"))
                .ToListAsync();

            var result = new List<SectionAverageDto>();

            foreach (var sectionName in sectionNames)
            {
                var sectionQuestionIds = questions
                    .Where(q => (q.SectionName ?? "General") == sectionName)
                    .Select(q => q.QuestionId)
                    .ToHashSet();

                decimal sectionMax = questions
                    .Where(q => sectionQuestionIds.Contains(q.QuestionId))
                    .Sum(q => q.Points ?? 0);

                var studentPercents = new List<decimal>();

                foreach (var attempt in attempts)
                {
                    var sectionAnswers = attempt.Answers
                        .Where(a => a.QuestionId.HasValue && sectionQuestionIds.Contains(a.QuestionId.Value))
                        .ToList();

                    if (sectionAnswers.Count == 0) continue;
                    if (!sectionAnswers.All(a => a.ObtainedPoints.HasValue)) continue; // section not fully scored for this student yet
                    if (sectionMax <= 0) continue;

                    decimal earned = sectionAnswers.Sum(a => a.ObtainedPoints ?? 0);
                    studentPercents.Add(earned / sectionMax * 100m);
                }

                result.Add(new SectionAverageDto
                {
                    SectionName = sectionName,
                    HasData = studentPercents.Count > 0,
                    AveragePercent = studentPercents.Count > 0 ? Math.Round(studentPercents.Average(), 1) : 0
                });
            }

            return result;
        }

        // ── For the exam-selector dropdown on ReportsTab's generic view ──
        public async Task<List<ExamOptionDto>> GetInstructorExamsAsync(string instructorId)
        {
            using var context = new AppDbContext();

            return await context.Exams
                .Where(e => e.InstructorId == instructorId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new ExamOptionDto
                {
                    ExamId = e.ExamId,
                    Title = e.Title ?? e.ExamName ?? "Untitled Exam"
                })
                .ToListAsync();
        }
    }

    public class ReportsSummaryDto
    {
        public bool HasClassAverage { get; set; }
        public decimal ClassAveragePercent { get; set; }
        public int GradedCount { get; set; }

        public bool HasCompletionRate { get; set; }
        public decimal CompletionRatePercent { get; set; }
        public int TotalAttempts { get; set; }

        public bool HasIntegrityIndex { get; set; }
        public decimal IntegrityIndexPercent { get; set; }

        public int GazeDeviationCount { get; set; }
        public int WindowSwitchCount { get; set; }
        public int ClipboardCount { get; set; }
        public int TotalInfractions { get; set; }

        public List<ReportRowDto> Rows { get; set; } = new();
    }

    public class ReportRowDto
    {
        public Guid AttemptId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string FinalScoreText { get; set; } = string.Empty;
        public bool IsPassing { get; set; }
        public int AnomalyCount { get; set; }
        public string RiskLevel { get; set; } = "Low";
    }

    public class SectionAverageDto
    {
        public string SectionName { get; set; } = string.Empty;
        public bool HasData { get; set; }
        public decimal AveragePercent { get; set; }
    }

    public class ExamOptionDto
    {
        public Guid ExamId { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}

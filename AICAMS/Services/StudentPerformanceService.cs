using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class StudentPerformanceService
    {
        private static readonly HashSet<string> AiGradedTypes = new() { "essay", "coding" };

        // ── Top metric cards ──────────────────────────────────────────────
        public async Task<PerformanceMetricsDto> GetMetricsAsync(string studentId)
        {
            using var context = new AppDbContext();

            // Concept Mastery Index reuses the exact same overall-average
            // calculation already built for StudentInformationTab, so the
            // two pages never disagree with each other.
            var profile = await new StudentInformationService().GetStudentProfileSummaryAsync(studentId);

            var result = new PerformanceMetricsDto
            {
                HasConceptMastery = profile.HasOverallAverage,
                ConceptMasteryPercent = profile.OverallAveragePercent
            };

            // AI Confidence Benchmark: of this student's AI-graded essay/coding
            // answers, what % were never overridden by an instructor? Real,
            // computable from GradedBy — unlike the class-wide "AI vs
            // Instructor" chart elsewhere, which has no baseline to compare yet.
            var currentInstructorId = SessionManager.CurrentInstructorId;
            var aiGradableAnswersQuery = context.StudentAnswers
                .Include(a => a.Question)
                .Include(a => a.Attempt)
                    .ThenInclude(att => att!.Exam)
                .Where(a => a.Attempt != null && a.Attempt.StudentId == studentId
                         && a.Question != null && AiGradedTypes.Contains(a.Question.QuestionType)
                         && a.GradedBy != null);

            if (!string.IsNullOrEmpty(currentInstructorId))
            {
                aiGradableAnswersQuery = aiGradableAnswersQuery.Where(a => a.Attempt!.Exam != null && a.Attempt.Exam.InstructorId == currentInstructorId);
            }

            var aiGradableAnswers = await aiGradableAnswersQuery.ToListAsync();

            if (aiGradableAnswers.Count > 0)
            {
                result.HasAiConfidence = true;
                int acceptedWithoutOverride = aiGradableAnswers.Count(a => a.GradedBy == "ai");
                result.AiConfidencePercent = Math.Round(
                    (decimal)acceptedWithoutOverride / aiGradableAnswers.Count * 100m, 0);
            }

            // Risk score — same flag-based approach used elsewhere
            // (StudentReportPage, ReportsTab), just aggregated across every
            // attempt this student has made instead of one.
            var attemptsQuery = context.StudentExamAttempts
                .Where(a => a.StudentId == studentId && (a.Status == "submitted" || a.Status == "graded"));

            if (!string.IsNullOrEmpty(currentInstructorId))
            {
                attemptsQuery = attemptsQuery.Where(a => a.Exam != null && a.Exam.InstructorId == currentInstructorId);
            }

            var attemptIds = await attemptsQuery
                .Select(a => a.AttemptId.ToString())
                .ToListAsync();

            if (attemptIds.Count > 0)
            {
                int totalFlags = await context.AuditLogs
                    .Where(l => l.EntityType == "StudentExamAttempt" && attemptIds.Contains(l.EntityId))
                    .CountAsync();

                double avgFlagsPerAttempt = (double)totalFlags / attemptIds.Count;

                result.HasRiskScore = true;
                result.RiskScore = totalFlags;
                result.RiskLevel = avgFlagsPerAttempt >= 5 ? "High" : avgFlagsPerAttempt >= 2 ? "Medium" : "Low";
            }

            return result;
        }

        // ── Question-type performance (replaces the fake skill-tag radar) ──
        // Real average % per question type, across every exam this student
        // has taken — not per-exam, since this page is about the student
        // as a whole.
        public async Task<List<QuestionTypePerformanceDto>> GetQuestionTypePerformanceAsync(string studentId)
        {
            using var context = new AppDbContext();

            var currentInstructorId = SessionManager.CurrentInstructorId;
            var answersQuery = context.StudentAnswers
                .Include(a => a.Question)
                .Include(a => a.Attempt)
                    .ThenInclude(att => att!.Exam)
                .Where(a => a.Attempt != null && a.Attempt.StudentId == studentId
                         && a.Question != null && a.ObtainedPoints.HasValue);

            if (!string.IsNullOrEmpty(currentInstructorId))
            {
                answersQuery = answersQuery.Where(a => a.Attempt!.Exam != null && a.Attempt.Exam.InstructorId == currentInstructorId);
            }

            var answers = await answersQuery.ToListAsync();

            var byType = answers
                .GroupBy(a => a.Question!.QuestionType)
                .Select(g => new QuestionTypePerformanceDto
                {
                    QuestionType = FormatQuestionType(g.Key),
                    AveragePercent = g.Sum(a => a.Question!.Points ?? 0) > 0
                        ? Math.Round(g.Sum(a => a.ObtainedPoints ?? 0) / g.Sum(a => a.Question!.Points ?? 0) * 100m, 1)
                        : 0
                })
                .OrderByDescending(t => t.AveragePercent)
                .ToList();

            return byType;
        }

        // ── Behavioral insights — only cards backed by real tracked data.
        // "Keystroke Dynamics" from the original mockup is deliberately
        // omitted; this system has no keystroke-biometric tracking at all,
        // and fabricating a "Stable" badge for it would be dishonest. ──
        public async Task<BehavioralInsightsDto> GetBehavioralInsightsAsync(string studentId)
        {
            using var context = new AppDbContext();

            var currentInstructorId = SessionManager.CurrentInstructorId;
            var attemptsQuery = context.StudentExamAttempts
                .Where(a => a.StudentId == studentId && (a.Status == "submitted" || a.Status == "graded"));

            if (!string.IsNullOrEmpty(currentInstructorId))
            {
                attemptsQuery = attemptsQuery.Where(a => a.Exam != null && a.Exam.InstructorId == currentInstructorId);
            }

            var attemptIds = await attemptsQuery
                .Select(a => a.AttemptId.ToString())
                .ToListAsync();

            var result = new BehavioralInsightsDto { SessionCount = attemptIds.Count };

            if (attemptIds.Count == 0) return result;

            var logs = await context.AuditLogs
                .Where(l => l.EntityType == "StudentExamAttempt" && attemptIds.Contains(l.EntityId))
                .ToListAsync();

            result.GazeDeviationCount = logs.Count(l => l.Action.Contains("NoFaceDetected") || l.Action.Contains("MultipleFaces") || l.Action.Contains("GazeDiverted"));
            result.WindowSwitchCount = logs.Count(l => l.Action.Contains("AltTab") || l.Action.Contains("WindowFocus"));
            result.ClipboardCount = logs.Count(l => l.Action.Contains("Clipboard"));

            return result;
        }

        // ── AI Historical Evaluations Ledger — condensed summary of
        // assessments already generated via PerformanceAssessmentService
        // on StudentReportPage. Does not generate anything new here. ──
        public async Task<List<EvaluationLedgerRowDto>> GetEvaluationLedgerAsync(string studentId)
        {
            using var context = new AppDbContext();

            var currentInstructorId = SessionManager.CurrentInstructorId;
            var assessments = await context.Set<AiAssessment>()
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.GeneratedAt)
                .ToListAsync();

            var result = new List<EvaluationLedgerRowDto>();

            foreach (var assessment in assessments)
            {
                var exam = await context.Exams.FirstOrDefaultAsync(e => e.ExamId == assessment.ExamId);
                if (exam == null) continue;
                if (!string.IsNullOrEmpty(currentInstructorId) && exam.InstructorId != currentInstructorId) continue;

                var attempt = await context.StudentExamAttempts
                    .Include(a => a.Answers)
                        .ThenInclude(ans => ans.Question)
                    .FirstOrDefaultAsync(a => a.StudentId == studentId && a.ExamId == assessment.ExamId);

                string scoreText = "—";
                if (attempt?.TotalScore != null)
                {
                    decimal maxPts = attempt.Answers
                        .Where(a => a.Question != null)
                        .Select(a => a.Question!)
                        .Distinct()
                        .Sum(q => q.Points ?? 0);
                    if (maxPts > 0)
                        scoreText = $"{attempt.TotalScore.Value:0.##} / {maxPts:0.##}";
                }

                int strengthsCount = CountBullets(assessment.Strengths);
                int weaknessesCount = CountBullets(assessment.Weaknesses);

                string snippet = assessment.AiSummary ?? string.Empty;
                if (snippet.Length > 160) snippet = snippet[..157] + "...";

                result.Add(new EvaluationLedgerRowDto
                {
                    ExamName = exam.ExamName ?? "Untitled Exam",
                    ScoreText = scoreText,
                    StrengthsCount = strengthsCount,
                    WeaknessesCount = weaknessesCount,
                    SummarySnippet = snippet
                });
            }

            return result;
        }

        private static int CountBullets(string? text) =>
            string.IsNullOrWhiteSpace(text) ? 0 : text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        private static string FormatQuestionType(string type) => type switch
        {
            "multiple_choice" => "Multiple Choice",
            "true_false" => "True/False",
            "identification" => "Identification",
            "enumeration" => "Enumeration",
            "essay" => "Essay",
            "coding" => "Code",
            _ => type
        };
    }

    public class PerformanceMetricsDto
    {
        public bool HasConceptMastery { get; set; }
        public decimal ConceptMasteryPercent { get; set; }

        public bool HasAiConfidence { get; set; }
        public decimal AiConfidencePercent { get; set; }

        public bool HasRiskScore { get; set; }
        public int RiskScore { get; set; }
        public string RiskLevel { get; set; } = "Low";
    }

    public class QuestionTypePerformanceDto
    {
        public string QuestionType { get; set; } = string.Empty;
        public decimal AveragePercent { get; set; }
    }

    public class BehavioralInsightsDto
    {
        public int SessionCount { get; set; }
        public int GazeDeviationCount { get; set; }
        public int WindowSwitchCount { get; set; }
        public int ClipboardCount { get; set; }
    }

    public class EvaluationLedgerRowDto
    {
        public string ExamName { get; set; } = string.Empty;
        public string ScoreText { get; set; } = string.Empty;
        public int StrengthsCount { get; set; }
        public int WeaknessesCount { get; set; }
        public string SummarySnippet { get; set; } = string.Empty;
    }
}

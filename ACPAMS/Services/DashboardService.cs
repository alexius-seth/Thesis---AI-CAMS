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
                Title = exam.ExamName ?? "Untitled Exam",
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
                .Where(e => e.Status != "archived")
                .Where(e => e.Attempts.Any(a => a.Status == "in_progress"))
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (exam == null)
            {
                exam = await context.Exams
                    .Include(e => e.Attempts)
                    .Where(e => e.InstructorId == instructorId)
                    .Where(e => e.Status != "archived")
                    .OrderByDescending(e => e.Status == "live" ? 1 : 0)
                    .ThenByDescending(e => e.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            if (exam == null) return null;

            var attempts = exam.Attempts ?? new List<StudentExamAttempt>();

            return new LiveExamDto
            {
                ExamId = exam.ExamId,
                Title = exam.ExamName ?? "Untitled Exam",
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
                .Include(q => q.Options)
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
                    if (ans == null || (ans.ObtainedPoints.HasValue && (ans.IsManualGrade || ans.GradedBy != "system"))) continue;

                    bool isCorrect = AutoCheckerService.IsAnswerCorrect(ans.AnswerText, q);

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
                double pct = maxExamPoints > 0 ? (double)(attempt.TotalScore!.Value / maxExamPoints) * 100 : (double)attempt.TotalScore!.Value;

                if (pct >= 90.0) dist.Band90to100++;
                else if (pct >= 80.0) dist.Band80to89++;
                else if (pct >= 70.0) dist.Band70to79++;
                else dist.BelowSeventy++;
            }

            return dist;
        }

        // ── Class Pass Rate + Average Score KPIs ──────────────────────
        // Both are computed as a % of the exam's maximum possible score,
        // across every attempt that already has a final TotalScore and is completed.
        private static decimal PASSING_PERCENT => AppSettingsService.PassingGrade;

        public async Task<ClassSummaryDto> GetClassSummaryAsync(Guid examId)
        {
            using var context = new AppDbContext();

            decimal maxExamPoints = await context.Questions
                .Where(q => q.ExamId == examId)
                .SumAsync(q => q.Points ?? 0);

            var scores = await context.StudentExamAttempts
                .Where(a => a.ExamId == examId &&
                            (a.Status == "submitted" || a.Status == "graded") &&
                            a.TotalScore.HasValue)
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

        // ── Top Identified Weaknesses (Live Aggregate Data) ───────────
        public async Task<List<WeaknessItemDto>> GetTopWeaknessesAsync(Guid examId)
        {
            using var context = new AppDbContext();

            var weaknessCandidates = new Dictionary<string, (string Category, int Count, int Total)>(StringComparer.OrdinalIgnoreCase);

            // 1. AI Assessment Weaknesses if any exist for this exam
            var aiAssessments = await context.Set<AiAssessment>()
                .Where(a => a.ExamId == examId && !string.IsNullOrEmpty(a.Weaknesses))
                .ToListAsync();

            int totalAiStudents = aiAssessments.Count;
            if (totalAiStudents > 0)
            {
                foreach (var ai in aiAssessments)
                {
                    var lines = (ai.Weaknesses ?? "")
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim().TrimStart('•', '-', '*').Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l));

                    foreach (var line in lines)
                    {
                        string topic = line;
                        string category = "General Concept";

                        if (line.Contains(':'))
                        {
                            var parts = line.Split(':', 2);
                            topic = parts[0].Trim();
                            category = parts[1].Trim();
                        }
                        else if (line.Contains('-'))
                        {
                            var parts = line.Split('-', 2);
                            topic = parts[0].Trim();
                            category = parts[1].Trim();
                        }

                        if (topic.Length > 40) topic = topic.Substring(0, 40) + "...";
                        if (category.Length > 30) category = category.Substring(0, 30);

                        if (!weaknessCandidates.ContainsKey(topic))
                            weaknessCandidates[topic] = (category, 0, totalAiStudents);

                        var cur = weaknessCandidates[topic];
                        weaknessCandidates[topic] = (cur.Category, cur.Count + 1, totalAiStudents);
                    }
                }
            }

            // 2. Query student answers and rubric evaluations for completed attempts
            var attempts = await context.StudentExamAttempts
                .Where(a => a.ExamId == examId && (a.Status == "submitted" || a.Status == "graded"))
                .Select(a => a.AttemptId)
                .ToListAsync();

            if (attempts.Count > 0)
            {
                // 2a. Rubric evaluations with low score (< 70% of criteria max)
                var rubricEvals = await context.Set<AiRubricEvaluation>()
                    .Include(r => r.Criteria)
                    .Include(r => r.Evaluation)
                        .ThenInclude(e => e.Answer)
                    .Where(r => r.Evaluation != null && r.Evaluation.Answer != null && attempts.Contains(r.Evaluation.Answer.AttemptId ?? Guid.Empty))
                    .ToListAsync();

                if (rubricEvals.Count > 0)
                {
                    var groupedCriteria = rubricEvals
                        .Where(r => r.Criteria != null && !string.IsNullOrWhiteSpace(r.Criteria.CriteriaName))
                        .GroupBy(r => r.Criteria!.CriteriaName!);

                    foreach (var grp in groupedCriteria)
                    {
                        int total = grp.Count();
                        int failed = grp.Count(r =>
                        {
                            decimal max = r.Criteria?.MaxScore ?? 10m;
                            decimal assigned = r.AssignedScore ?? 0m;
                            return max > 0 && (assigned / max) < 0.7m;
                        });

                        if (failed > 0 && !weaknessCandidates.ContainsKey(grp.Key))
                        {
                            weaknessCandidates[grp.Key] = ("Rubric Evaluation", failed, total);
                        }
                    }
                }

                // 2b. Question-level failure rates
                var answers = await context.StudentAnswers
                    .Include(a => a.Question)
                    .Where(a => attempts.Contains(a.AttemptId ?? Guid.Empty) && a.Question != null)
                    .ToListAsync();

                if (answers.Count > 0)
                {
                    var groupedByTopic = answers
                        .GroupBy(a => !string.IsNullOrWhiteSpace(a.Question!.SectionName)
                            ? a.Question.SectionName
                            : (!string.IsNullOrWhiteSpace(a.Question.QuestionType) ? a.Question.QuestionType : "General Assessment"));

                    foreach (var grp in groupedByTopic)
                    {
                        int total = grp.Count();
                        int failed = grp.Count(a =>
                            a.ExecutionStatus == "failed" ||
                            a.ExecutionStatus == "runtime_error" ||
                            (a.Question!.Points > 0 && (a.ObtainedPoints ?? 0) < (a.Question.Points.Value * 0.6m)));

                        if (failed > 0)
                        {
                            string topic = grp.Key switch
                            {
                                "coding" => "Code Implementation",
                                "multiple_choice" => "Multiple Choice Concepts",
                                "true_false" => "Theoretical Knowledge",
                                "essay" => "Conceptual Explanations",
                                _ => grp.Key
                            };

                            string category = grp.FirstOrDefault(a => !string.IsNullOrEmpty(a.LanguageUsed))?.LanguageUsed?.ToUpper()
                                ?? grp.FirstOrDefault(a => !string.IsNullOrEmpty(a.Question?.ProgrammingLanguage))?.Question?.ProgrammingLanguage?.ToUpper()
                                ?? "Assessment Section";

                            if (!weaknessCandidates.ContainsKey(topic))
                            {
                                weaknessCandidates[topic] = (category, failed, total);
                            }
                        }
                    }
                }
            }

            return weaknessCandidates
                .Select(kvp =>
                {
                    int pct = kvp.Value.Total > 0 ? (int)Math.Round((double)kvp.Value.Count / kvp.Value.Total * 100) : 0;
                    return new
                    {
                        Topic = kvp.Key,
                        kvp.Value.Category,
                        FailPct = Math.Min(100, Math.Max(1, pct)),
                        Count = kvp.Value.Count
                    };
                })
                .Where(x => x.FailPct > 0)
                .OrderByDescending(x => x.FailPct)
                .ThenByDescending(x => x.Count)
                .Take(3)
                .Select((x, idx) => new WeaknessItemDto
                {
                    Rank = idx + 1,
                    Topic = x.Topic,
                    Category = x.Category,
                    FailPercentage = x.FailPct
                })
                .ToList();
        }
    }

    public class LiveExamDto
    {
        public Guid ExamId { get; set; }
        public string ExamName { get; set; } = string.Empty;
        public string Title { get => ExamName; set => ExamName = value; }
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

    public class WeaknessItemDto
    {
        public int Rank { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int FailPercentage { get; set; }
        public string StatText => $"{FailPercentage}% fail";
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class ExamStatusService
    {
        private readonly AppDbContext _context;

        public ExamStatusService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ExamStatusDto>> GetExamsWithStatusAsync(string instructorId, bool archivedOnly = false)
        {
            var query = _context.Exams
                .Include(e => e.Attempts)
                    .ThenInclude(a => a.Answers)
                .Include(e => e.Questions)
                .Where(e => e.InstructorId == instructorId);

            if (archivedOnly)
            {
                query = query.Where(e => e.Status == "archived");
            }
            else
            {
                query = query.Where(e => e.Status != "archived");
            }

            var exams = await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            var result = new List<ExamStatusDto>();

            foreach (var exam in exams)
            {
                var activeAttempts = exam.Attempts?.Where(a => a.Status == "in_progress").Count() ?? 0;
                var submittedAttempts = exam.Attempts?.Where(a => a.Status == "submitted").Count() ?? 0;
                var totalAttempts = exam.Attempts?.Count ?? 0;

                string status;
                if (exam.Status == "archived")
                    status = "archived";
                else if (activeAttempts > 0)
                    status = "live";
                else if (submittedAttempts > 0 || totalAttempts > 0)
                    status = "done";
                else
                    status = "pending";

                // ── Grading progress, for the "Recently Completed" action button ──
                bool hasSubjective = exam.Questions != null &&
                    exam.Questions.Any(q => q.QuestionType == "essay" || q.QuestionType == "coding");

                var subjectiveQuestionIds = exam.Questions?
                    .Where(q => q.QuestionType == "essay" || q.QuestionType == "coding")
                    .Select(q => q.QuestionId)
                    .ToHashSet() ?? new HashSet<Guid>();

                var gradableAttempts = exam.Attempts?
                    .Where(a => a.Status == "submitted" || a.Status == "graded")
                    .ToList() ?? new List<StudentExamAttempt>();

                int pendingReviewCount = 0;
                int readyToFinalizeCount = 0;
                int gradedCount = 0;

                foreach (var attempt in gradableAttempts)
                {
                    if (attempt.Status == "graded")
                    {
                        gradedCount++;
                        continue;
                    }

                    if (!hasSubjective)
                        continue; // nothing to manually review — objective exams auto-score

                    var subjectiveAnswers = attempt.Answers?
                        .Where(a => a.QuestionId.HasValue && subjectiveQuestionIds.Contains(a.QuestionId.Value))
                        .ToList() ?? new List<StudentAnswer>();

                    bool allSubjectiveScored = subjectiveQuestionIds.Count > 0 &&
                        subjectiveQuestionIds.All(qid => subjectiveAnswers.Any(a => a.QuestionId == qid && a.ObtainedPoints.HasValue));

                    if (allSubjectiveScored)
                        readyToFinalizeCount++;
                    else
                        pendingReviewCount++;
                }

                string actionLabel;
                string gradingStatusText;
                bool gradingStatusIsWarning;

                if (!hasSubjective)
                {
                    actionLabel = "View Report";
                    gradingStatusText = "✓  100% Pre-Scored";
                    gradingStatusIsWarning = false;
                }
                else if (pendingReviewCount > 0)
                {
                    actionLabel = "Review Submissions";
                    gradingStatusText = $"⚠  {pendingReviewCount} Pending Review{(pendingReviewCount == 1 ? "" : "s")}";
                    gradingStatusIsWarning = true;
                }
                else if (readyToFinalizeCount > 0)
                {
                    actionLabel = "Finalize Grades";
                    gradingStatusText = $"⚠  {readyToFinalizeCount} Ready to Finalize";
                    gradingStatusIsWarning = true;
                }
                else
                {
                    actionLabel = "View Report";
                    gradingStatusText = "✓  Grades Finalized";
                    gradingStatusIsWarning = false;
                }

                result.Add(new ExamStatusDto
                {
                    ExamId = exam.ExamId,
                    Title = exam.ExamName ?? "Untitled Exam",
                    ExamCode = exam.ExamCode ?? "N/A",
                    CourseCode = exam.CourseCode ?? "N/A",
                    Type = GetExamType(exam),
                    Status = status,
                    StatusDisplay = status switch
                    {
                        "archived" => "ARCHIVED",
                        "live" => "LIVE NOW",
                        "done" => "COMPLETED",
                        "pending" => "UPCOMING",
                        _ => "UNKNOWN"
                    },
                    ActiveStudents = activeAttempts,
                    TotalStudents = totalAttempts,
                    CreatedAt = exam.CreatedAt,
                    DurationMinutes = exam.DurationMinutes ?? 0,
                    TotalPoints = exam.TotalPoints ?? 0,
                    HasAttempts = totalAttempts > 0,
                    Attempts = exam.Attempts?.ToList() ?? new List<StudentExamAttempt>(),
                    HasSubjectiveQuestions = hasSubjective,
                    PendingReviewCount = pendingReviewCount,
                    ReadyToFinalizeCount = readyToFinalizeCount,
                    ActionLabel = actionLabel,
                    GradingStatusText = gradingStatusText,
                    GradingStatusIsWarning = gradingStatusIsWarning
                });
            }

            return result;
        }

        public async Task<int> GetActiveStudentsCountAsync(Guid examId)
        {
            return await _context.StudentExamAttempts
                .Where(a => a.ExamId == examId && a.Status == "in_progress")
                .CountAsync();
        }


        public async Task ArchiveExamAsync(Guid examId)
        {
            var exam = await _context.Exams.FindAsync(examId);
            if (exam != null)
            {
                exam.Status = "archived";
                await _context.SaveChangesAsync();
            }
        }

        public async Task UnarchiveExamAsync(Guid examId)
        {
            var exam = await _context.Exams.FindAsync(examId);
            if (exam != null)
            {
                exam.Status = "active";
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteExamAsync(Guid examId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var questionIds = await _context.Questions
                    .Where(q => q.ExamId == examId)
                    .Select(q => q.QuestionId)
                    .ToListAsync();

                var attemptIds = await _context.StudentExamAttempts
                    .Where(a => a.ExamId == examId)
                    .Select(a => a.AttemptId)
                    .ToListAsync();

                var answerIds = await _context.StudentAnswers
                    .Where(a => (a.QuestionId.HasValue && questionIds.Contains(a.QuestionId.Value))
                             || (a.AttemptId.HasValue && attemptIds.Contains(a.AttemptId.Value)))
                    .Select(a => a.AnswerId)
                    .ToListAsync();

                var rubricIds = await _context.Rubrics
                    .Where(r => r.QuestionId.HasValue && questionIds.Contains(r.QuestionId.Value))
                    .Select(r => r.RubricId)
                    .ToListAsync();

                var evalIds = await _context.AiEvaluations
                    .Where(e => (e.AnswerId.HasValue && answerIds.Contains(e.AnswerId.Value))
                             || (e.RubricUsedId.HasValue && rubricIds.Contains(e.RubricUsedId.Value)))
                    .Select(e => e.EvaluationId)
                    .ToListAsync();

                var criteriaIds = await _context.RubricCriteria
                    .Where(c => c.RubricId.HasValue && rubricIds.Contains(c.RubricId.Value))
                    .Select(c => c.CriteriaId)
                    .ToListAsync();

                // 1. Delete AI Rubric Evaluations and AI Evaluations
                if (evalIds.Count > 0)
                {
                    await _context.AiRubricEvaluations
                        .Where(r => r.EvaluationId.HasValue && evalIds.Contains(r.EvaluationId.Value))
                        .ExecuteDeleteAsync();

                    await _context.AiEvaluations
                        .Where(e => evalIds.Contains(e.EvaluationId))
                        .ExecuteDeleteAsync();
                }

                // 2. Delete AI Jobs and Student Answers
                if (answerIds.Count > 0)
                {
                    await _context.AiJobs
                        .Where(j => j.AnswerId.HasValue && answerIds.Contains(j.AnswerId.Value))
                        .ExecuteDeleteAsync();

                    await _context.StudentAnswers
                        .Where(a => answerIds.Contains(a.AnswerId))
                        .ExecuteDeleteAsync();
                }

                // 3. Delete AI Assessments
                await _context.AiAssessments
                    .Where(a => a.ExamId == examId)
                    .ExecuteDeleteAsync();

                // 4. Delete Audit Logs for Exam and Attempts
                var entityIdStrings = attemptIds.Select(id => id.ToString()).Concat(new[] { examId.ToString() }).ToList();
                await _context.AuditLogs
                    .Where(l => l.EntityId != null && entityIdStrings.Contains(l.EntityId))
                    .ExecuteDeleteAsync();

                // 5. Delete Attachments
                if (questionIds.Count > 0 || answerIds.Count > 0)
                {
                    await _context.Attachments
                        .Where(at => (at.EntityId.HasValue && questionIds.Contains(at.EntityId.Value))
                                  || (at.EntityId.HasValue && answerIds.Contains(at.EntityId.Value)))
                        .ExecuteDeleteAsync();
                }

                // 6. Delete Student Attempts
                if (attemptIds.Count > 0)
                {
                    await _context.StudentExamAttempts
                        .Where(a => attemptIds.Contains(a.AttemptId))
                        .ExecuteDeleteAsync();
                }

                // 7. Delete Rubric Levels, Criteria, and Rubrics
                if (criteriaIds.Count > 0)
                {
                    await _context.RubricLevels
                        .Where(l => l.CriteriaId.HasValue && criteriaIds.Contains(l.CriteriaId.Value))
                        .ExecuteDeleteAsync();

                    await _context.RubricCriteria
                        .Where(c => criteriaIds.Contains(c.CriteriaId))
                        .ExecuteDeleteAsync();
                }

                if (rubricIds.Count > 0)
                {
                    await _context.Rubrics
                        .Where(r => rubricIds.Contains(r.RubricId))
                        .ExecuteDeleteAsync();
                }

                // 8. Delete Question Options and Questions
                if (questionIds.Count > 0)
                {
                    await _context.QuestionOptions
                        .Where(o => questionIds.Contains(o.QuestionId))
                        .ExecuteDeleteAsync();

                    await _context.Questions
                        .Where(q => questionIds.Contains(q.QuestionId))
                        .ExecuteDeleteAsync();
                }

                // 9. Delete the Exam
                await _context.Exams
                    .Where(e => e.ExamId == examId)
                    .ExecuteDeleteAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ── "Finalize Grades" action: lock in scores for every fully-graded attempt ──
        public async Task FinalizeExamGradesAsync(Guid examId)
        {
            var attempts = await _context.StudentExamAttempts
                .Include(a => a.Answers)
                .Where(a => a.ExamId == examId && a.Status == "submitted")
                .ToListAsync();

            foreach (var attempt in attempts)
            {
                if (attempt.Answers.Count > 0 && attempt.Answers.All(a => a.ObtainedPoints.HasValue))
                {
                    attempt.TotalScore = attempt.Answers.Sum(a => a.ObtainedPoints ?? 0);
                    attempt.Status = "graded";
                }
            }

            await _context.SaveChangesAsync();
        }

        private string GetExamType(Exam exam)
        {
            if (exam.Questions == null || !exam.Questions.Any())
                return "Unknown";

            var types = exam.Questions.Select(q => q.QuestionType).Distinct().ToList();
            if (types.Count == 1)
                return types.First() switch
                {
                    "coding" => "Coding",
                    "essay" => "Essay",
                    "multiple_choice" => "Multiple Choice",
                    "true_false" => "True/False",
                    "identification" => "Identification",
                    "enumeration" => "Enumeration",
                    _ => types.First()
                };
            return "Mixed";
        }
    }

    public class ExamStatusDto
    {
        public Guid ExamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ExamCode { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public int ActiveStudents { get; set; }
        public int TotalStudents { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime DateCreated => CreatedAt;
        public DateTime? ArchivedDate { get; set; }
        public int DurationMinutes { get; set; }
        public decimal TotalPoints { get; set; }
        public bool HasAttempts { get; set; }
        public List<StudentExamAttempt> Attempts { get; set; } = new();

        // ── Grading progress (populated for "done" exams) ──────
        public bool HasSubjectiveQuestions { get; set; }
        public int PendingReviewCount { get; set; }
        public int ReadyToFinalizeCount { get; set; }

        /// <summary>"Review Submissions" | "Finalize Grades" | "View Report" — drives the
        /// Recently Completed action button per item 5's rules.</summary>
        public string ActionLabel { get; set; } = "View Report";

        public string GradingStatusText { get; set; } = string.Empty;
        public bool GradingStatusIsWarning { get; set; }

        // ── Display helpers for FacultyHomePage cards ──────────
        public bool IsArchived => Status == "archived";
        public bool IsLive => !IsArchived && Status == "live";
        public bool IsPending => !IsArchived && Status == "pending";
        public bool IsDone => !IsArchived && Status == "done";
        public bool IsActiveOrUpcoming => !IsArchived && (IsLive || IsPending);

        /// <summary>Pending and Done cards both get Archive/Delete actions.</summary>
        public bool ShowArchiveDelete => !IsArchived && (IsPending || IsDone);

        public string StudentsProgressText => $"{ActiveStudents} / {TotalStudents}";
    }
}
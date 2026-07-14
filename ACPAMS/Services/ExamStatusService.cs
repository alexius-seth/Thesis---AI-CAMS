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

        public async Task<List<ExamStatusDto>> GetExamsWithStatusAsync(string instructorId)
        {
            var exams = await _context.Exams
                .Include(e => e.Attempts)
                .Where(e => e.InstructorId == instructorId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            var result = new List<ExamStatusDto>();

            foreach (var exam in exams)
            {
                var activeAttempts = exam.Attempts?.Where(a => a.Status == "in_progress").Count() ?? 0;
                var submittedAttempts = exam.Attempts?.Where(a => a.Status == "submitted").Count() ?? 0;
                var totalAttempts = exam.Attempts?.Count ?? 0;

                string status;
                if (activeAttempts > 0)
                    status = "live";
                else if (submittedAttempts > 0 || totalAttempts > 0)
                    status = "done";
                else
                    status = "pending";

                result.Add(new ExamStatusDto
                {
                    ExamId = exam.ExamId,
                    Title = exam.Title ?? exam.ExamName ?? "Untitled Exam",
                    ExamCode = exam.ExamCode ?? "N/A",
                    CourseCode = exam.CourseCode ?? "N/A",
                    Type = GetExamType(exam),
                    Status = status,
                    StatusDisplay = status switch
                    {
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
                    Attempts = exam.Attempts?.ToList() ?? new List<StudentExamAttempt>()
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

        public async Task<int> GetTotalFlagsCountAsync(Guid examId)
        {
            var attemptIds = await _context.StudentExamAttempts
                .Where(a => a.ExamId == examId)
                .Select(a => a.AttemptId.ToString())
                .ToListAsync();

            return await _context.AuditLogs
                .Where(l => l.EntityType == "StudentExamAttempt"
                            && attemptIds.Contains(l.EntityId)
                            && !l.Action.Contains("HeadTurned")
                            && !l.Action.Contains("HeadTilted"))
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

        public async Task DeleteExamAsync(Guid examId)
        {
            var exam = await _context.Exams
                .Include(e => e.Questions)
                .ThenInclude(q => q.Options)
                .Include(e => e.Attempts)
                .ThenInclude(a => a.Answers)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            if (exam != null)
            {
                // Remove all related data
                _context.Exams.Remove(exam);
                await _context.SaveChangesAsync();
            }
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
        public int DurationMinutes { get; set; }
        public decimal TotalPoints { get; set; }
        public bool HasAttempts { get; set; }
        public List<StudentExamAttempt> Attempts { get; set; } = new();

        // ── Display helpers for FacultyHomePage cards ──────────
        public bool IsLive => Status == "live";
        public bool IsPending => Status == "pending";
        public bool IsDone => Status == "done";

        /// <summary>Pending and Done cards both get Archive/Delete actions.</summary>
        public bool ShowArchiveDelete => IsPending || IsDone;

        public string StudentsProgressText => $"{ActiveStudents} / {TotalStudents}";
    }
}
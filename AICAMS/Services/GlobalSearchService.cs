using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;

namespace ACPAMS.Services
{
    public class GlobalSearchService
    {
        public async Task<List<SearchResultDto>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return new List<SearchResultDto>();

            var currentInstructorId = SessionManager.CurrentInstructorId;
            if (string.IsNullOrEmpty(currentInstructorId))
                return new List<SearchResultDto>();

            string term = query.Trim();
            using var db = new AppDbContext();

            // 1. Search Exams (scoped exclusively to active instructor, non-archived)
            var exams = await db.Exams
                .AsNoTracking()
                .Where(e => e.InstructorId == currentInstructorId && e.Status != "archived")
                .Where(e => (e.ExamName != null && EF.Functions.Like(e.ExamName, $"%{term}%")) ||
                            (e.CourseCode != null && EF.Functions.Like(e.CourseCode, $"%{term}%")))
                .Take(5)
                .Select(e => new SearchResultDto
                {
                    Type = "Assessment",
                    Title = e.ExamName ?? "Untitled Exam",
                    Subtitle = $"{e.CourseCode ?? "No Code"} • Status: {e.Status}",
                    Icon = "\uE8A5",
                    TargetRoute = "Exam",
                    TargetId = e.ExamId.ToString()
                })
                .ToListAsync();

            // 2. Search Students (scoped to students with submission attempts on current instructor's exams)
            var students = await db.Students
                .AsNoTracking()
                .Where(s => s.ExamAttempts.Any(a => a.Exam != null && a.Exam.InstructorId == currentInstructorId))
                .Where(s => EF.Functions.Like(s.StudentId, $"%{term}%") ||
                            EF.Functions.Like(s.FirstName, $"%{term}%") ||
                            EF.Functions.Like(s.LastName, $"%{term}%"))
                .Take(5)
                .Select(s => new SearchResultDto
                {
                    Type = "Student",
                    Title = $"{s.FirstName} {s.LastName}".Trim(),
                    Subtitle = $"ID: {s.StudentId} • {s.Program ?? ""} {s.YearLevel ?? ""}".Trim(),
                    Icon = "\uE77B",
                    TargetRoute = "Student",
                    TargetId = s.StudentId
                })
                .ToListAsync();

            return exams.Concat(students).ToList();
        }
    }

    public class SearchResultDto
    {
        public string Type { get; set; } = string.Empty; // Assessment or Student
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Icon { get; set; } = "\uE721";
        public string TargetRoute { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;

        public System.Windows.Media.Brush TypeBadgeBg => Type == "Assessment"
            ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EFF6FF"))
            : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0FDF4"));

        public System.Windows.Media.Brush TypeBadgeFg => Type == "Assessment"
            ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2"))
            : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#15803D"));
    }
}

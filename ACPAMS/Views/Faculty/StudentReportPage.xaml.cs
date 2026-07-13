using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Views.Faculty
{
    public partial class StudentReportPage : UserControl
    {
        public StudentReportViewModel Report { get; set; } = new();
        private Guid _attemptId;

        public StudentReportPage(Guid attemptId)
        {
            _attemptId = attemptId;
            DataContext = this;
            InitializeComponent();
            LoadReport();
        }

        private async void LoadReport()
        {
            try
            {
                using var context = new AppDbContext();

                // Get the attempt with related data
                var attempt = await context.StudentExamAttempts
                    .Include(a => a.Student)
                    .Include(a => a.Exam)
                    .Include(a => a.Answers)
                        .ThenInclude(ans => ans.Question)
                    .FirstOrDefaultAsync(a => a.AttemptId == _attemptId);

                if (attempt == null)
                {
                    MessageBox.Show("Attempt not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Get all flags for this attempt (excluding head movement events)
                var flags = await context.AuditLogs
                    .Where(l => l.EntityId == _attemptId.ToString()
                                && l.EntityType == "StudentExamAttempt"
                                && !l.Action.Contains("HeadTurned")
                                && !l.Action.Contains("HeadTilted"))
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();

                // Build the report
                Report.AttemptId = _attemptId;
                Report.StudentName = attempt.Student != null
                    ? $"{attempt.Student.FirstName} {attempt.Student.LastName}"
                    : "Unknown Student";
                Report.StudentId = attempt.Student?.StudentId ?? "N/A";
                Report.Program = attempt.Student?.Program ?? "N/A";
                Report.Section = "N/A";
                Report.ExamName = attempt.Exam?.ExamName ?? attempt.Exam?.Title ?? "Unknown Exam";
                Report.ExamCode = attempt.Exam?.ExamCode ?? "N/A";
                Report.StartedAt = attempt.StartedAt ?? DateTime.Now;
                Report.SubmittedAt = attempt.SubmittedAt;
                Report.Status = attempt.Status ?? "in_progress";

                // Calculate scores - convert decimal to double
                var answers = attempt.Answers?.ToList() ?? new List<StudentAnswer>();
                Report.TotalScore = (double)(answers.Sum(a => a.ObtainedPoints ?? 0));
                Report.MaxPossibleScore = (double)(answers.Sum(a => a.Question?.Points ?? 0));

                // Calculate percentage
                if (Report.MaxPossibleScore > 0)
                {
                    Report.TotalScorePercent = Report.TotalScore / Report.MaxPossibleScore;
                }
                else
                {
                    Report.TotalScorePercent = 0;
                }

                // Cheating score from monitoring logs
                var cheatingScore = await context.AuditLogs
                    .Where(l => l.EntityId == _attemptId.ToString()
                                && l.EntityType == "StudentExamAttempt"
                                && l.Details.Contains("Score:"))
                    .OrderByDescending(l => l.CreatedAt)
                    .FirstOrDefaultAsync();

                if (cheatingScore != null)
                {
                    var scoreMatch = Regex.Match(cheatingScore.Details, @"Score:\s*([\d.]+)");
                    if (scoreMatch.Success && double.TryParse(scoreMatch.Groups[1].Value, out double score))
                    {
                        Report.CheatingScore = score;
                    }
                }

                // Risk level based on flags
                Report.TotalFlags = flags.Count;
                Report.RiskLevel = flags.Count >= 5 ? "high" : flags.Count >= 3 ? "medium" : "low";

                // Flag history
                Report.FlagHistory = flags.Select(f => new FlagEventDto
                {
                    Timestamp = f.CreatedAt,
                    EventType = f.Action ?? "Unknown",
                    Description = f.Details ?? string.Empty,
                    Severity = GetSeverity(f.Action ?? "")
                }).ToList();

                // Question answers
                Report.QuestionAnswers = answers.Select((a, index) => new QuestionAnswerDto
                {
                    Number = index + 1,
                    Text = a.Question?.Text ?? "Unknown Question",
                    Type = a.Question?.QuestionType ?? "Unknown",
                    StudentAnswer = a.AnswerText ?? a.SourceCode ?? "No answer provided",
                    ObtainedPoints = a.ObtainedPoints.HasValue ? (double)a.ObtainedPoints.Value : (double?)null,
                    MaxPoints = a.Question?.Points.HasValue == true ? (double)a.Question.Points.Value : 0,
                    Status = a.ObtainedPoints.HasValue ? "graded" : "pending"
                }).ToList();

                // If we have no cheating score history, add some sample data
                if (Report.FlagHistory.Count > 0 && Report.CheatingScore == 0)
                {
                    // Generate a simple cheating score based on flags
                    Report.CheatingScore = Math.Min(100, Report.FlagHistory.Count * 15 + 5);
                }

                // Update UI
                DataContext = null;
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetSeverity(string action)
        {
            return action switch
            {
                "NoFaceDetected" or "AltTabAttempt" or "WindowFocusLost" or "MultipleFacesDetected" => "high",
                "ClipboardAttempt" => "medium",
                _ => "low"
            };
        }

        private void BackToMonitoring_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToMonitoringStatic();
        }
    }
}
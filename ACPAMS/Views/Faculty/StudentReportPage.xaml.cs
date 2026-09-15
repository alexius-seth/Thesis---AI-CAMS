using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using ACPAMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Views.Faculty
{
    public partial class StudentReportPage : UserControl
    {
        public StudentReportViewModel Report { get; set; } = new();
        private Guid _attemptId;
        private string? _studentId;
        private Guid? _examId;

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
                            .ThenInclude(q => q!.Options)
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
                Report.IsFinalized = attempt.Status == "graded";

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
                    StudentAnswer = a.Question?.QuestionType == "multiple_choice"
                        ? AutoCheckerService.FormatOptionDisplay(a.AnswerText, a.Question?.Options)
                        : (a.AnswerText ?? a.SourceCode ?? "No answer provided"),
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

                // ── Performance assessment (Strengths/Weaknesses) ────────
                // Remember these for GenerateAssessment_Click, then load
                // whatever assessment (if any) was already generated before.
                _studentId = attempt.StudentId;
                _examId = attempt.ExamId;

                if (_studentId != null && _examId.HasValue)
                {
                    var existingAssessment = await new PerformanceAssessmentService()
                        .GetExistingAssessmentAsync(_studentId, _examId.Value);

                    if (existingAssessment != null)
                    {
                        Report.HasAssessment = true;
                        Report.Strengths = existingAssessment.Strengths;
                        Report.Weaknesses = existingAssessment.Weaknesses;
                        Report.AssessmentSummary = existingAssessment.Summary;
                        Report.AssessmentGeneratedAt = existingAssessment.GeneratedAt;
                    }
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
            MainWindow.NavigateBackStatic();
        }

        private void StudentName_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_studentId != null)
                MainWindow.Navigate(new StudentInformationTab(_studentId));
        }

        private async void GenerateAssessment_Click(object sender, RoutedEventArgs e)
        {
            if (!Report.IsFinalized)
            {
                MessageBox.Show(
                    "Grades for this attempt aren't finalized yet. Finalize grading in " +
                    "GradingTab first, so this assessment reflects confirmed scores, not " +
                    "AI-suggested ones still pending your review.",
                    "Not Finalized", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (sender is Button button) button.IsEnabled = false;

            try
            {
                var result = await new PerformanceAssessmentService().GenerateAssessmentAsync(_attemptId);

                Report.HasAssessment = true;
                Report.Strengths = result.Strengths;
                Report.Weaknesses = result.Weaknesses;
                Report.AssessmentSummary = result.Summary;
                Report.AssessmentGeneratedAt = DateTime.Now;

                DataContext = null;
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating assessment: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button btn) btn.IsEnabled = true;
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"{(string.IsNullOrWhiteSpace(Report.StudentName) ? "Student" : Report.StudentName.Replace(" ", "_"))}_{(string.IsNullOrWhiteSpace(Report.ExamCode) ? "Exam" : Report.ExamCode)}_Report.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    PdfExportService.ExportStudentReport(Report, saveDialog.FileName);
                    MessageBox.Show("Student performance report exported successfully!", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export PDF: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
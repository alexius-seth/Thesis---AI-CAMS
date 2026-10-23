using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ACPAMS.Views.Student
{
    public partial class ExamEntryPage : UserControl
    {
        private readonly ExamAccessService _accessService = new();
        private DispatcherTimer? _approvalTimer;
        private Exam? _pendingExam;
        private string? _pendingExamCode;

        public ExamEntryPage()
        {
            InitializeComponent();
            ACPAMS.MainWindow.Resize(540, 520);
            LoadStudentName();
            Unloaded += (s, e) => _approvalTimer?.Stop();
        }

        private void LoadStudentName()
        {
            if (SessionManager.CurrentStudent != null)
                StudentNameRun.Text = SessionManager.CurrentStudent.FirstName;
        }

        private async void ProceedButton_Click(object sender, RoutedEventArgs e)
        {
            string examCode = ExamCodeBox.Text.Trim();

            if (string.IsNullOrEmpty(examCode))
            {
                MessageBox.Show("Please enter the exam code provided by your instructor.",
                    "Missing Exam Code", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProceedBtn.IsEnabled = false;
            ProceedBtn.Content = "Verifying...";

            try
            {
                using var context = new AppDbContext();

                var exam = await context.Exams
                    .Include(e => e.AuthorizedStudents)
                    .FirstOrDefaultAsync(e => e.ExamCode != null && e.ExamCode.Trim().ToLower() == examCode.ToLower());

                if (exam == null)
                {
                    MessageBox.Show(
                        "Exam code not found. Please verify with your proctor.",
                        "Invalid Code", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (exam.Status == "archived")
                {
                    MessageBox.Show(
                        "This examination has concluded and is now archived.",
                        "Exam Concluded", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (exam.Status == "draft")
                {
                    MessageBox.Show(
                        "This examination is currently in draft mode and is not accepting submissions.",
                        "Exam Not Available", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var studentId = SessionManager.CurrentStudent?.StudentId;
                if (string.IsNullOrEmpty(studentId))
                {
                    MessageBox.Show("Student session expired. Please log in again.", "Session Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check Access Control for Irregular/Off-Schedule Students
                var accessStatus = await _accessService.CheckAccessStatusAsync(exam.ExamId, studentId);

                if (accessStatus == ExamAccessService.AccessResult.Denied)
                {
                    MessageBox.Show(
                        "Access to this examination has been denied for your account. Please contact your instructor.",
                        "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (accessStatus == ExamAccessService.AccessResult.RequiresRequest || accessStatus == ExamAccessService.AccessResult.PendingApproval)
                {
                    await _accessService.RequestAccessAsync(exam.ExamId, studentId);
                    ShowWaitingApprovalView(exam, examCode);
                    return;
                }

                // If Allowed, proceed to start or resume exam attempt
                await StartOrResumeExamAsync(exam, examCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ProceedBtn.IsEnabled = true;
                ProceedBtn.Content = "Proceed to Exam";
            }
        }

        private void ShowWaitingApprovalView(Exam exam, string examCode)
        {
            _pendingExam = exam;
            _pendingExamCode = examCode;

            InputPanel.Visibility = Visibility.Collapsed;
            WaitingApprovalPanel.Visibility = Visibility.Visible;

            ApprovalExamTitle.Text = $"{exam.CourseCode} - {exam.ExamName}";
            ApprovalExamDetails.Text = string.Join(" • ", new[]
            {
                !string.IsNullOrWhiteSpace(exam.Section) ? $"Section: {exam.Section}" : null,
                !string.IsNullOrWhiteSpace(exam.ClassSchedule) ? exam.ClassSchedule : null
            }.Where(s => s != null));

            ApprovalAcademicPeriod.Text = string.Join(" | ", new[]
            {
                exam.AcademicYear,
                exam.Semester
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            // Start 2-second real-time polling timer
            _approvalTimer?.Stop();
            _approvalTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _approvalTimer.Tick += async (s, e) =>
            {
                var studentId = SessionManager.CurrentStudent?.StudentId;
                if (string.IsNullOrEmpty(studentId) || _pendingExam == null) return;

                var status = await _accessService.CheckAccessStatusAsync(_pendingExam.ExamId, studentId);

                if (status == ExamAccessService.AccessResult.Allowed)
                {
                    _approvalTimer.Stop();
                    await StartOrResumeExamAsync(_pendingExam, _pendingExamCode ?? examCode);
                }
                else if (status == ExamAccessService.AccessResult.Denied)
                {
                    _approvalTimer.Stop();
                    MessageBox.Show("Your request for examination access was denied by the instructor.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CancelRequest_Click(this, new RoutedEventArgs());
                }
            };
            _approvalTimer.Start();
        }

        private void CancelRequest_Click(object sender, RoutedEventArgs e)
        {
            _approvalTimer?.Stop();
            _pendingExam = null;
            _pendingExamCode = null;

            WaitingApprovalPanel.Visibility = Visibility.Collapsed;
            InputPanel.Visibility = Visibility.Visible;
        }

        private async Task StartOrResumeExamAsync(Exam exam, string examCode)
        {
            using var context = new AppDbContext();

            var existingAttempt = await context.StudentExamAttempts
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync(a =>
                    a.StudentId == SessionManager.CurrentStudent!.StudentId
                    && a.ExamId == exam.ExamId);

            // Case A - Completed
            if (existingAttempt != null && (existingAttempt.Status == "submitted" || existingAttempt.Status == "graded" || existingAttempt.SubmittedAt != null))
            {
                MessageBox.Show("You have already submitted this exam.",
                    "Exam Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Case B - In-Progress (Crash Recovery / Resume)
            if (existingAttempt != null && existingAttempt.Status == "in_progress" && existingAttempt.SubmittedAt == null)
            {
                int totalDurationMinutes = (exam.DurationMinutes ?? 60) + (existingAttempt.ExtraTimeMinutes ?? 0);
                var deadline = (existingAttempt.StartedAt ?? DateTime.Now).AddMinutes(totalDurationMinutes);
                var remainingSeconds = (int)(deadline - DateTime.Now).TotalSeconds;

                if (remainingSeconds <= 0)
                {
                    existingAttempt.Status = "submitted";
                    existingAttempt.SubmittedAt = DateTime.Now;
                    await context.SaveChangesAsync();

                    MessageBox.Show("Your exam time has expired. This session has been closed.",
                        "Time Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Resume existing session
                SessionManager.CurrentAttempt = existingAttempt;
                SessionManager.CurrentExam = exam;

                ACPAMS.MainWindow.EnterKioskFullscreenStatic();
                ACPAMS.MainWindow.Navigate(new StudentExamPage());
                return;
            }

            // Case C - Brand New Attempt
            var attempt = new Models.StudentExamAttempt
            {
                AttemptId = Guid.NewGuid(),
                StudentId = SessionManager.CurrentStudent!.StudentId,
                ExamId = exam.ExamId,
                ExamCodeUsed = examCode,
                StartedAt = DateTime.Now,
                Status = "in_progress",
                ExtraTimeMinutes = 0,
                IpAddress = GetLocalIpAddress(),
                DeviceInfo = Environment.MachineName
            };

            context.StudentExamAttempts.Add(attempt);
            await context.SaveChangesAsync();

            SessionManager.CurrentAttempt = attempt;
            SessionManager.CurrentExam = exam;

            ACPAMS.MainWindow.EnterKioskFullscreenStatic();
            ACPAMS.MainWindow.Navigate(new StudentExamPage());
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch { }
            return "unknown";
        }

        private void ProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            _approvalTimer?.Stop();
            ACPAMS.MainWindow.Navigate(new StudentProfilePage());
        }

        private void LogOutBtn_Click(object sender, RoutedEventArgs e)
        {
            _approvalTimer?.Stop();
            SessionManager.CurrentStudent = null;
            SessionManager.CurrentUser = null;
            ACPAMS.MainWindow.Navigate(new Views.Auth.LoginPage());
        }
    }
}


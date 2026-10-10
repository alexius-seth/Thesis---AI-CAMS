using ACPAMS.Data;
using ACPAMS.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Student
{
    public partial class ExamEntryPage : UserControl
    {
        public ExamEntryPage()
        {
            InitializeComponent();
            ACPAMS.MainWindow.Resize(520, 480);
            LoadStudentName();
        }

        private void LoadStudentName()
        {
            if (SessionManager.CurrentStudent != null)
                StudentNameRun.Text = SessionManager.CurrentStudent.FirstName;
        }

        private async void ProceedButton_Click(object sender, RoutedEventArgs e)
        {
            string examCode = ExamCodeBox.Text.Trim().ToUpper();

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
                    .FirstOrDefaultAsync(e => e.ExamCode == examCode
                                           && e.Status == "active");

                if (exam == null)
                {
                    MessageBox.Show(
                        "Invalid or inactive exam code. Please check with your instructor.",
                        "Invalid Code", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

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
            ACPAMS.MainWindow.Navigate(new StudentProfilePage());
        }

        private void LogOutBtn_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
        }
    }
}

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
                    .FirstOrDefaultAsync(a =>
                        a.StudentId == SessionManager.CurrentStudent!.StudentId
                        && a.ExamId == exam.ExamId
                        && a.Status == "in_progress");

                if (existingAttempt != null)
                {
                    MessageBox.Show("You already have an ongoing attempt for this exam.",
                        "Existing Attempt", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var attempt = new Models.StudentExamAttempt
                {
                    AttemptId = Guid.NewGuid(),
                    StudentId = SessionManager.CurrentStudent!.StudentId,
                    ExamId = exam.ExamId,
                    ExamCodeUsed = examCode,
                    StartedAt = DateTime.Now,
                    Status = "in_progress",
                    IpAddress = GetLocalIpAddress(),
                    DeviceInfo = Environment.MachineName
                };

                context.StudentExamAttempts.Add(attempt);
                await context.SaveChangesAsync();

                SessionManager.CurrentAttempt = attempt;
                SessionManager.CurrentExam = exam;

                ACPAMS.MainWindow.Resize(1280, 800);
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
            SessionManager.Clear();
            ACPAMS.MainWindow.Resize(520, 600);
            ACPAMS.MainWindow.Navigate(new Views.Auth.LoginPage());
        }
    }
}

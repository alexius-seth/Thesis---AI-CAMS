using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using ACPAMS.Views.Faculty.Tabs;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Views.Faculty
{
    public partial class DashboardPage : UserControl
    {
        private Exam? _currentExam;

        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Instructor must be logged in and stored in SessionManager
            // (set during login, before DashboardPage is ever shown).
            var instructor = SessionManager.CurrentInstructor;
            if (instructor == null)
            {
                StatusChip.Visibility = Visibility.Collapsed;
                return;
            }

            string instructorId = instructor.InstructorId;

            var service = new DashboardService();
            var live = await service.GetLiveExamAsync(instructorId);

            if (live != null)
            {
                using (var context = new AppDbContext())
                {
                    _currentExam = await context.Exams.FirstOrDefaultAsync(x => x.ExamId == live.ExamId);
                }

                StatusChip.Visibility = Visibility.Visible;
                StatusChipLabel.Text = $"  ·  {live.CourseCode} {live.Title}";

                if (TabContent.Content is OverviewTab overview)
                {
                    await overview.LoadAsync(live);
                }
            }
            else
            {
                StatusChip.Visibility = Visibility.Collapsed;
            }
        }

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToHomeStatic();
        }

        private void NavigateToCreateExam(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToCreateExamStatic();
        }

        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            // Already here - do nothing
        }

        private void NavigateToMonitoring(object sender, RoutedEventArgs e)
        {
            // MainWindow.NavigateToMonitoringStatic() takes no arguments —
            // MonitoringTab reads the active exam from SessionManager.CurrentExam.
            if (_currentExam != null)
            {
                SessionManager.CurrentExam = _currentExam;
                MainWindow.NavigateToMonitoringStatic();
            }
        }
    }
}
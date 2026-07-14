using ACPAMS.Services;
using ACPAMS.ViewModels;
using ACPAMS.Views.Faculty.Tabs;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty
{
    public partial class FacultyHomePage : UserControl
    {
        private readonly FacultyHomePageViewModel _viewModel;

        public FacultyHomePage()
        {
            InitializeComponent();

            // SessionManager.CurrentInstructor is set at login (see LoginPage.xaml.cs).
            // NOTE: if SessionManager doesn't resolve, add the correct
            // "using ACPAMS.XYZ;" for wherever it actually lives.
            var instructorId = SessionManager.CurrentInstructor?.InstructorId;

            _viewModel = new FacultyHomePageViewModel(instructorId);
            DataContext = _viewModel;

            Loaded += FacultyHomePage_Loaded;
        }

        private async void FacultyHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadExamsAsync();
        }

        // ─────────────────────────────────────────────
        // Sidebar / quick-nav (unchanged from before)
        // ─────────────────────────────────────────────

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            // Already on Home - do nothing
        }

        private void OpenDashboardPage(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToDashboardStatic();
        }

        private void OpenCreateExamPage(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow.NavigateToCreateExamStatic();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}\n\n{ex.InnerException?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenMonitoringTab(object sender, RoutedEventArgs e)
        {
            var liveExam = _viewModel.Exams.FirstOrDefault(x => x.IsLive);

            if (liveExam == null)
            {
                ToastNotification.Show("No Current Active Exam", Application.Current.MainWindow, 3);
                return;
            }

            MainWindow.Navigate(new MonitoringTab(liveExam.ExamId));
        }

        private void OpenStudentReportTab(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToReportsStatic();
        }

        private void ManageLiveSession(object sender, RoutedEventArgs e)
        {
            OpenMonitoringTab(sender, e);
        }

        private void ViewReport(object sender, RoutedEventArgs e)
        {
            OpenStudentReportTab(sender, e);
        }
    }
}
using ACPAMS.Services;
using ACPAMS.ViewModels;
using ACPAMS.Views.Faculty.Tabs;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ACPAMS.Views.Faculty
{
    public partial class FacultyHomePage : UserControl, IRefreshableView
    {
        private readonly FacultyHomePageViewModel _viewModel;
        private readonly DispatcherTimer _autoRefreshTimer;

        public FacultyHomePage()
        {
            InitializeComponent();

            var instructorId = SessionManager.CurrentInstructor?.InstructorId;
            _viewModel = new FacultyHomePageViewModel(instructorId);
            DataContext = _viewModel;

            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoRefreshTimer.Tick += async (s, e) => await _viewModel.LoadExamsAsync();

            Loaded += FacultyHomePage_Loaded;
            Unloaded += FacultyHomePage_Unloaded;
        }

        private async void FacultyHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown += Window_PreviewKeyDown;

            _autoRefreshTimer.Start();
            await _viewModel.LoadExamsAsync();
        }

        private void FacultyHomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown -= Window_PreviewKeyDown;

            _autoRefreshTimer.Stop();
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await _viewModel.LoadExamsAsync();
            }
        }

        // Called by MainWindow.ShowPage on every navigation to this page.
        // FacultyHomePage is cached as a singleton in MainWindow, so this is
        // the main reason "leave and come back" wasn't showing fresh data —
        // Loaded alone wasn't a reliable enough signal for a reused instance.
        public async System.Threading.Tasks.Task RefreshAsync()
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

        private void OpenArchivedExamsPage(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToArchivedExamsStatic();
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

        private void OpenRegistrationCodeSettings_Click(object sender, RoutedEventArgs e)
        {
            var modal = new FacultyCodeSettingsModal
            {
                Owner = Window.GetWindow(this)
            };
            modal.ShowDialog();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToSettingsStatic();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
        }
    }
}
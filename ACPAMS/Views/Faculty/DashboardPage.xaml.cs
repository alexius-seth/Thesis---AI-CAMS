using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ACPAMS.Services;
using ACPAMS.Views.Faculty.Tabs;

namespace ACPAMS.Views.Faculty
{
    public partial class DashboardPage : UserControl, IRefreshableView
    {
        // If set, the dashboard shows this specific exam (e.g. opened via
        // "View Dashboard" from a Faculty Home card) instead of auto-detecting
        // whichever exam is currently live for the instructor.
        private readonly Guid? _explicitExamId;
        private Guid? _currentExamId;
        private readonly DispatcherTimer _autoRefreshTimer;

        public DashboardPage() : this(null) { }

        public DashboardPage(Guid? examId)
        {
            InitializeComponent();
            _explicitExamId = examId;

            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoRefreshTimer.Tick += async (s, e) => await LoadDashboardAsync();

            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Catch Ctrl+R anywhere in the window while this page is showing.
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown += Window_PreviewKeyDown;

            _autoRefreshTimer.Start();

            await LoadDashboardAsync();
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
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
                await LoadDashboardAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadDashboardAsync()
        {
            var service = new DashboardService();

            LiveExamDto? live = _explicitExamId.HasValue
                ? await service.GetExamByIdAsync(_explicitExamId.Value)
                : await GetLiveExamForCurrentInstructorAsync(service);

            if (live != null)
            {
                _currentExamId = live.ExamId;

                StatusChip.Visibility = Visibility.Visible;
                StatusChipLabel.Text = $"  ·  {live.CourseCode} {live.Title}";

                if (TabContent.Content is OverviewTab overview)
                {
                    await overview.LoadAsync(live);
                }
            }
            else
            {
                _currentExamId = null;
                StatusChip.Visibility = Visibility.Collapsed;
            }
        }

        private async System.Threading.Tasks.Task<LiveExamDto?> GetLiveExamForCurrentInstructorAsync(DashboardService service)
        {
            var instructor = SessionManager.CurrentInstructor;
            if (instructor == null) return null;

            return await service.GetLiveExamAsync(instructor.InstructorId);
        }

        // Called by MainWindow.ShowPage on every navigation to this page —
        // including returning to the cached singleton instance via Back or
        // the sidebar link, where Loaded may not reliably refire.
        public async System.Threading.Tasks.Task RefreshAsync()
        {
            await LoadDashboardAsync();
        }

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToHomeStatic();
        }

        private void NavigateToCreateExam(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToCreateExamStatic();
        }

        private void NavigateToArchivedExams(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToArchivedExamsStatic();
        }

        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            // Already here - do nothing
        }

        private void NavigateToMonitoring(object sender, RoutedEventArgs e)
        {
            if (_currentExamId.HasValue)
            {
                // Same pattern FacultyHomePage.OpenMonitoringTab uses, so this
                // page participates correctly in MainWindow's navigation history.
                MainWindow.Navigate(new MonitoringTab(_currentExamId.Value));
            }
            else
            {
                ToastNotification.Show("No active exam to monitor", Application.Current.MainWindow, 3);
            }
        }

        private void NavigateToGrading(object sender, RoutedEventArgs e)
        {
            if (_currentExamId.HasValue)
            {
                MainWindow.Navigate(new GradingTab(_currentExamId.Value));
            }
            else
            {
                ToastNotification.Show("No exam selected to grade", Application.Current.MainWindow, 3);
            }
        }

        private void NavigateToReports(object sender, RoutedEventArgs e)
        {
            if (_currentExamId.HasValue)
            {
                MainWindow.Navigate(new ReportsTab(_currentExamId.Value));
            }
            else
            {
                MainWindow.Navigate(new ReportsTab(null));
            }
        }

        private void NavigateToSettings(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToSettingsStatic();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Clear();
            MainWindow.Resize(520, 600);
            MainWindow.Navigate(new Views.Auth.LoginPage());
        }
    }
}
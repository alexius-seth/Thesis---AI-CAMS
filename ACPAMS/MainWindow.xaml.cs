using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ACPAMS.Views.Faculty;
using ACPAMS.Views.Faculty.Tabs;

namespace ACPAMS
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        // Page instances
        private FacultyHomePage? _homePage;
        private DashboardPage? _dashboardPage;
        private CreateExamPage? _createExamPage;
        private MonitoringTab? _monitoringControl;
        private ReportsTab? _reportsControl;
        private GradingTab? _gradingControl;
        private FacultySettingsPage? _settingsPage;
        private ArchivedExamsPage? _archivedExamsPage;
        private string _selectedTaskCategory = "Examination";

        // ─── Navigation history ──────────────────────────────────────
        // Tracks where the user came from so Back can return them to the
        // actual previous page, instead of hardcoding Home.
        private readonly Stack<object> _navigationHistory = new();
        private bool _isNavigatingBack = false;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            SetStandardMaximized();

            // Start at Login
            ShowPage(new Views.Auth.LoginPage());
        }

        // ─── Public Navigation Methods ──────────────────────────────

        public void NavigateToHome()
        {
            if (_homePage == null)
                _homePage = new FacultyHomePage();
            ShowPage(_homePage);
            ClearHistory();
        }

        public void NavigateToDashboard()
        {
            if (_dashboardPage == null)
                _dashboardPage = new DashboardPage();
            ShowPage(_dashboardPage);
        }

        public void NavigateToCreateExam(Guid? examId = null)
        {
            if (examId.HasValue)
            {
                // Bypass TaskCategoryModal when editing existing exam
                _createExamPage = new CreateExamPage(examId.Value);
                ShowPage(_createExamPage);
                return;
            }

            // Show the TaskCategoryModal first for new exam
            var modal = new TaskCategoryModal();
            modal.Owner = this;

            if (modal.ShowDialog() == true)
            {
                _selectedTaskCategory = modal.SelectedCategory;
                OpenCreateExamPage(_selectedTaskCategory);
            }
        }

        public void OpenCreateExamPage(string category)
        {
            _createExamPage = new CreateExamPage(category);
            ShowPage(_createExamPage);
        }

        public void NavigateToMonitoring(Guid? examId = null)
        {
            if (examId.HasValue)
                _monitoringControl = new MonitoringTab(examId.Value);
            else if (_monitoringControl == null)
                _monitoringControl = new MonitoringTab();

            ShowPage(_monitoringControl);
        }

        public void NavigateToReports(Guid? examId = null)
        {
            _reportsControl = examId.HasValue ? new ReportsTab(examId.Value) : new ReportsTab();
            ShowPage(_reportsControl);
        }

        public void NavigateToGrading()
        {
            if (_gradingControl == null)
                _gradingControl = new GradingTab();
            ShowPage(_gradingControl);
        }

        public void NavigateToSettings()
        {
            if (_settingsPage == null)
                _settingsPage = new FacultySettingsPage();
            ShowPage(_settingsPage);
        }

        public void NavigateToArchivedExams()
        {
            if (_archivedExamsPage == null)
                _archivedExamsPage = new ArchivedExamsPage();
            ShowPage(_archivedExamsPage);
        }

        public void ShowTaskCategoryModal()
        {
            var modal = new TaskCategoryModal();
            modal.Owner = this;

            if (modal.ShowDialog() == true)
            {
                _selectedTaskCategory = modal.SelectedCategory;
                OpenCreateExamPage(_selectedTaskCategory);
            }
        }

        // ─── Back navigation ──────────────────────────────────────
        // Returns to whichever page the user was actually on before the
        // current one — not a hardcoded destination.
        public void NavigateBack()
        {
            if (_navigationHistory.Count == 0)
            {
                if (Services.SessionManager.IsInstructor)
                    NavigateToHome();
                else
                    ShowPage(new Views.Auth.LoginPage());
                return;
            }

            var previous = _navigationHistory.Pop();

            _isNavigatingBack = true;
            try
            {
                ShowPage(previous);
            }
            finally
            {
                _isNavigatingBack = false;
            }
        }

        public void ClearHistory()
        {
            _navigationHistory.Clear();
        }

        public void ClearSessionState()
        {
            _navigationHistory.Clear();
            _homePage = null;
            _dashboardPage = null;
            _createExamPage = null;
            _monitoringControl = null;
            _reportsControl = null;
            _gradingControl = null;
            _settingsPage = null;
            _archivedExamsPage = null;
            MainContent.Content = null;
        }

        public static void Logout()
        {
            Instance?.ClearSessionState();
            SetStandardMaximizedStatic();
            Navigate(new Views.Auth.LoginPage());
        }

        // ─── Static Navigation Methods ──────────────────────────────

        public static void Navigate(object content)
        {
            Instance?.ShowPage(content);
        }

        public static void NavigateToHomeStatic()
        {
            Instance?.NavigateToHome();
        }

        public static void NavigateToFacultyHomeStatic()
        {
            Instance?.NavigateToHome();
        }

        public static void NavigateToDashboardStatic()
        {
            Instance?.NavigateToDashboard();
        }

        public static void NavigateToCreateExamStatic(Guid? examId = null)
        {
            Instance?.NavigateToCreateExam(examId);
        }

        public static void NavigateToMonitoringStatic(Guid? examId = null)
        {
            Instance?.NavigateToMonitoring(examId);
        }

        public static void NavigateToReportsStatic(Guid? examId = null)
        {
            Instance?.NavigateToReports(examId);
        }

        public static void NavigateToGradingStatic()
        {
            Instance?.NavigateToGrading();
        }

        public static void NavigateToSettingsStatic()
        {
            Instance?.NavigateToSettings();
        }

        public static void NavigateToArchivedExamsStatic()
        {
            Instance?.NavigateToArchivedExams();
        }

        public static void NavigateToStudentProfileStatic(string studentId, NavigationOrigin origin = NavigationOrigin.Search, Guid? previousExamId = null)
        {
            Navigate(new StudentInformationTab(studentId, origin, previousExamId));
        }

        public static void NavigateToStudentReport(Guid attemptId)
        {
            var reportPage = new StudentReportPage(attemptId);
            Instance?.ShowPage(reportPage);
        }

        public static void NavigateBackStatic()
        {
            Instance?.NavigateBack();
        }

        public static void ClearNavigationHistory()
        {
            Instance?.ClearHistory();
        }

        // ─── Dynamic Window Mode Management ──────────────────────────

        public void SetStandardMaximized()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            Topmost = false;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Maximized;
        }

        public static void SetStandardMaximizedStatic()
        {
            Instance?.SetStandardMaximized();
        }

        public void EnterKioskFullscreen()
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowState = WindowState.Maximized;
        }

        public static void EnterKioskFullscreenStatic()
        {
            Instance?.EnterKioskFullscreen();
        }

        public void ExitKioskFullscreen()
        {
            SetStandardMaximized();
        }

        public static void ExitKioskFullscreenStatic()
        {
            Instance?.ExitKioskFullscreen();
        }

        public static void Resize(double width, double height)
        {
            if (Instance == null) return;
            if (Instance.WindowState == WindowState.Maximized) return;
            Instance.Width = width;
            Instance.Height = height;
            Instance.Left = (SystemParameters.PrimaryScreenWidth - width) / 2;
            Instance.Top = (SystemParameters.PrimaryScreenHeight - height) / 2;
        }

        // ─── Private Helper ──────────────────────────────────────────

        private async void ShowPage(object content)
        {
            // Record where we came from, unless this call IS a "Back" navigation
            // (that would just re-push the page we're leaving and break the trail).
            if (!_isNavigatingBack && MainContent.Content != null && MainContent.Content != content)
            {
                _navigationHistory.Push(MainContent.Content);
            }

            MainContent.Content = content;

            // Force a fresh data load on every navigation — including returning
            // to a cached instance (Home, Dashboard, Reports) via Back or a
            // sidebar link. Loaded alone isn't a reliable enough signal for
            // "the user just arrived here and wants current data."
            if (content is IRefreshableView refreshable)
            {
                await refreshable.RefreshAsync();
            }
        }
    }
}
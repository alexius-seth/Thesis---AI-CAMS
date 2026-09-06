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
        private FacultyHomePage _homePage;
        private DashboardPage _dashboardPage;
        private CreateExamPage _createExamPage;
        private MonitoringTab _monitoringControl;
        private ReportsTab _reportsControl;
        private GradingTab _gradingControl;
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

            // Start at Login
            ShowPage(new Views.Auth.LoginPage());
        }

        // ─── Public Navigation Methods ──────────────────────────────

        public void NavigateToHome()
        {
            if (_homePage == null)
                _homePage = new FacultyHomePage();
            ShowPage(_homePage);
        }

        public void NavigateToDashboard()
        {
            if (_dashboardPage == null)
                _dashboardPage = new DashboardPage();
            ShowPage(_dashboardPage);
        }

        public void NavigateToCreateExam()
        {
            // Show the TaskCategoryModal first
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

        public void NavigateToMonitoring()
        {
            if (_monitoringControl == null)
                _monitoringControl = new MonitoringTab();
            ShowPage(_monitoringControl);
        }

        public void NavigateToReports()
        {
            if (_reportsControl == null)
                _reportsControl = new ReportsTab();
            ShowPage(_reportsControl);
        }

        public void NavigateToGrading()
        {
            if (_gradingControl == null)
                _gradingControl = new GradingTab();
            ShowPage(_gradingControl);
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

        // ─── Static Navigation Methods ──────────────────────────────

        public static void Navigate(object content)
        {
            Instance?.ShowPage(content);
        }

        public static void NavigateToHomeStatic()
        {
            Instance?.NavigateToHome();
        }

        public static void NavigateToDashboardStatic()
        {
            Instance?.NavigateToDashboard();
        }

        public static void NavigateToCreateExamStatic()
        {
            Instance?.NavigateToCreateExam();
        }

        public static void NavigateToMonitoringStatic()
        {
            Instance?.NavigateToMonitoring();
        }

        public static void NavigateToReportsStatic()
        {
            Instance?.NavigateToReports();
        }

        public static void NavigateToGradingStatic()
        {
            Instance?.NavigateToGrading();
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

        public static void Resize(double width, double height)
        {
            if (Instance == null) return;
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
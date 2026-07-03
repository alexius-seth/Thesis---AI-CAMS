using System.Windows;
using System.Windows.Controls;
using ACPAMS.Views.Faculty.Tabs;

namespace ACPAMS.Views.Faculty
{
    public partial class Window1 : Window
    {
        private FacultyHomePage _homePage;
        private DashboardPage _dashboardPage;
        private CreateExamPage _createExamPage;
        private MonitoringTab _monitoringControl;
        private ReportsTab _reportsControl;

        public Window1()
        {
            InitializeComponent();
            NavigateToHome();
        }

        public void NavigateToHome()
        {
            if (_homePage == null)
                _homePage = new FacultyHomePage();
            MainContent.Content = _homePage;
        }

        public void NavigateToDashboard()
        {
            if (_dashboardPage == null)
                _dashboardPage = new DashboardPage();
            MainContent.Content = _dashboardPage;
        }

        public void NavigateToCreateExam()
        {
            if (_createExamPage == null)
                _createExamPage = new CreateExamPage();
            MainContent.Content = _createExamPage;
        }

        public void NavigateToMonitoring()
        {
            if (_monitoringControl == null)
                _monitoringControl = new MonitoringTab();
            MainContent.Content = _monitoringControl;
        }

        public void NavigateToReports()
        {
            if (_reportsControl == null)
                _reportsControl = new ReportsTab();
            MainContent.Content = _reportsControl;
        }
    }
}
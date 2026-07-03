using ACPAMS.Views.Faculty;
using ACPAMS.Views.Faculty.Tabs;
using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty
{
    public partial class FacultyHomePage : UserControl
    {
        public FacultyHomePage()
        {
            InitializeComponent();
        }

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            // Already on Home - do nothing
        }

        private void OpenDashboardPage(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToDashboard();
        }

        private void OpenCreateExamPage(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToCreateExam();
        }

        private void OpenMonitoringTab(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToMonitoring();
        }

        private void OpenStudentReportTab(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToReports();
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
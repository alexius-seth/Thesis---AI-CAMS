using ACPAMS.Views.Faculty.Tabs;
using System;
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
            ACPAMS.MainWindow.Navigate(new DashboardPage());
        }

        private void OpenCreateExamPage(object sender, RoutedEventArgs e)
        {
            try
            {
                ACPAMS.MainWindow.Navigate(new CreateExamPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}\n\n{ex.InnerException?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenMonitoringTab(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Navigate(new MonitoringTab());
        }

        private void OpenStudentReportTab(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Navigate(new StudentPerformanceReportTab());
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
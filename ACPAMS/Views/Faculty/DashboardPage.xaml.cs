using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty
{
    public partial class DashboardPage : UserControl
    {
        public DashboardPage()
        {
            InitializeComponent();
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
    }
}
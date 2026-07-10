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
            ACPAMS.MainWindow.Navigate(new FacultyHomePage());
        }

        private void NavigateToCreateExam(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Navigate(new CreateExamPage());
        }

        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
           
        }
    }
}
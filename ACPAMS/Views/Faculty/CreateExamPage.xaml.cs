using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty
{
    public partial class CreateExamPage : UserControl
    {
        public CreateExamPage()
        {
            InitializeComponent();
        }

        // ============================================
        // NAVIGATION - Calls Window1
        // ============================================

        // 1. Navigate to Home
        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToHome();
        }

        // 2. Navigate to Dashboard
        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToDashboard();
        }

        // 3. Stay on Create Exam
        private void NavigateToCreateExam(object sender, RoutedEventArgs e)
        {
            // Already here - do nothing
        }
    }
}
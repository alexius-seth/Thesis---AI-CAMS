using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty
{
    public partial class StudentPerformanceReportTab : UserControl
    {
        public StudentPerformanceReportTab()
        {
            InitializeComponent();
        }

        private void BackToHome_Click(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Navigate(new FacultyHomePage());
        }
    }
}
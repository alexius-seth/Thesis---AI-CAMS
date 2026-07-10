using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty.Tabs
{
    public partial class ReportsTab : UserControl
    {
        public ReportsTab()
        {
            InitializeComponent();
        }

        private void BackToHome_Click(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Navigate(new ACPAMS.Views.Faculty.FacultyHomePage());
        }
    }
}
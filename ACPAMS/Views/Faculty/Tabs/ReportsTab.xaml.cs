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
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToHome();
        }
    }
}
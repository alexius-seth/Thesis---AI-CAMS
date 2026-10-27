using System.Windows;
using System.Windows.Input;
using ACPAMS.ViewModels;

namespace ACPAMS.Views
{
    public partial class SetupWizardWindow : Window
    {
        public SetupWizardViewModel ViewModel => (SetupWizardViewModel)DataContext;

        public SetupWizardWindow()
        {
            InitializeComponent();

            ViewModel.RequestClose += (success) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (success && !(Application.Current.MainWindow is MainWindow))
                    {
                        var mainWindow = new MainWindow();
                        Application.Current.MainWindow = mainWindow;
                        mainWindow.Show();

                        App.StartAiJobProcessor();
                    }

                    try
                    {
                        DialogResult = success;
                    }
                    catch (System.InvalidOperationException)
                    {
                        // Thrown if window was displayed via .Show() instead of .ShowDialog()
                    }

                    Close();
                });
            };

            // Set initial password in PasswordBox if already present in ViewModel
            if (!string.IsNullOrEmpty(ViewModel.Password))
            {
                PwdBox.Password = ViewModel.Password;
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.Password = PwdBox.Password;
        }
    }
}


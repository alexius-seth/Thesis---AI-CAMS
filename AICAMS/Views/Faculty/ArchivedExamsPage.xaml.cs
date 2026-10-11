using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ACPAMS.Services;
using ACPAMS.ViewModels;

namespace ACPAMS.Views.Faculty
{
    public partial class ArchivedExamsPage : UserControl, IRefreshableView
    {
        private readonly ArchivedExamsViewModel _viewModel;
        private readonly DispatcherTimer _autoRefreshTimer;

        public ArchivedExamsPage()
        {
            InitializeComponent();

            var instructorId = SessionManager.CurrentInstructor?.InstructorId ?? string.Empty;
            _viewModel = new ArchivedExamsViewModel(instructorId);
            DataContext = _viewModel;

            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _autoRefreshTimer.Tick += async (s, e) => await _viewModel.LoadArchivedExamsAsync();

            Loaded += ArchivedExamsPage_Loaded;
            Unloaded += ArchivedExamsPage_Unloaded;
        }

        private async void ArchivedExamsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown += Window_PreviewKeyDown;

            _autoRefreshTimer.Start();
            await _viewModel.LoadArchivedExamsAsync();
        }

        private void ArchivedExamsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown -= Window_PreviewKeyDown;

            _autoRefreshTimer.Stop();
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await _viewModel.LoadExamsAsync();
            }
        }

        public async Task RefreshAsync()
        {
            await _viewModel.LoadExamsAsync();
        }

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToHomeStatic();
        }

        private void OpenDashboardPage(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToDashboardStatic();
        }

        private void OpenCreateExamPage(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToCreateExamStatic();
        }

        private void OpenRegistrationCodeSettings_Click(object sender, RoutedEventArgs e)
        {
            var modal = new FacultyCodeSettingsModal
            {
                Owner = Window.GetWindow(this)
            };
            modal.ShowDialog();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToSettingsStatic();
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Clear();
            MainWindow.ClearNavigationHistory();
            MainWindow.Resize(520, 600);
            MainWindow.Navigate(new Views.Auth.LoginPage());
        }
    }
}

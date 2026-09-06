using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ACPAMS.Services;
using ACPAMS.Views.Auth;

namespace ACPAMS.Views.Student
{
    public partial class ExamSubmissionConfirmationPage : UserControl
    {
        private readonly DispatcherTimer _redirectTimer;
        private int _secondsRemaining = 5;
        private bool _hasRedirected = false;

        public ExamSubmissionConfirmationPage()
        {
            InitializeComponent();

            _redirectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _redirectTimer.Tick += RedirectTimer_Tick;

            Loaded += (s, e) =>
            {
                MainWindow.ClearNavigationHistory();
                _redirectTimer.Start();
            };

            Unloaded += (s, e) => _redirectTimer.Stop();
        }

        private void RedirectTimer_Tick(object? sender, EventArgs e)
        {
            _secondsRemaining--;

            if (_secondsRemaining > 0)
            {
                CountdownText.Text = $"Redirecting to login in {_secondsRemaining} second{(_secondsRemaining == 1 ? "" : "s")}...";
            }
            else
            {
                _redirectTimer.Stop();
                CountdownText.Text = "Redirecting to login...";
                RedirectToLogin();
            }
        }

        private void RedirectToLogin()
        {
            if (_hasRedirected) return;
            _hasRedirected = true;

            _redirectTimer.Stop();
            SessionManager.Clear();
            MainWindow.ClearNavigationHistory();
            MainWindow.Resize(520, 600);
            MainWindow.Navigate(new LoginPage());
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Block keyboard navigation back to the exam
            if (e.Key == Key.Back || (e.Key == Key.Left && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
            {
                e.Handled = true;
            }
        }
    }
}

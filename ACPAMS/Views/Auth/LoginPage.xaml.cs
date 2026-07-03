using ACPAMS.Data;
using ACPAMS.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Windows;
using System.Windows.Controls;
using ACPAMS.Views.Faculty;

namespace ACPAMS.Views.Auth
{
    public partial class LoginPage : UserControl
    {
        private readonly AuthService _authService;
        private bool _passwordVisible = false;

        public LoginPage()
        {
            InitializeComponent();
            _authService = new AuthService();
            ACPAMS.MainWindow.Resize(520, 600);
        }

        private async void LogInButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = _passwordVisible
                ? PasswordVisibleBox.Text
                : PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter your username/ID and password.",
                    "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogInBtn.IsEnabled = false;
            LogInBtn.Content = "Logging in...";

            try
            {
                var user = await _authService.LoginAsync(username, password);

                if (user == null)
                {
                    MessageBox.Show("Invalid username or password. Please try again.",
                        "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SessionManager.CurrentUser = user;

                using var context = new AppDbContext();
                if (user.Role == "student")
                {
                    SessionManager.CurrentStudent = await context.Students
                        .FirstOrDefaultAsync(s => s.UserId == user.UserId);

                    ACPAMS.MainWindow.Resize(520, 480);
                    ACPAMS.MainWindow.Navigate(new Student.ExamEntryPage());
                }
                else if (user.Role == "instructor")
                {
                    SessionManager.CurrentInstructor = await context.Instructors
                        .FirstOrDefaultAsync(i => i.UserId == user.UserId);

                    ACPAMS.MainWindow.Resize(1280, 800);
                    ACPAMS.MainWindow.Navigate(new FacultyHomePage());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LogInBtn.IsEnabled = true;
                LogInBtn.Content = "Log In";
            }
        }

        private void TogglePasswordBtn_Click(object sender, RoutedEventArgs e)
        {
            _passwordVisible = !_passwordVisible;
            if (_passwordVisible)
            {
                PasswordVisibleBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordVisibleBox.Visibility = Visibility.Visible;
            }
            else
            {
                PasswordBox.Password = PasswordVisibleBox.Text;
                PasswordVisibleBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
            }
        }

        private void ForgotPasswordBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Please contact your administrator to reset your password.",
                "Forgot Password", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SignUpBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you signing up as a Student?\n\nClick YES for Student, NO for Faculty.",
                "Select Role", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ACPAMS.MainWindow.Resize(520, 680);
                ACPAMS.MainWindow.Navigate(new StudentSignUpPage());
            }
            else
            {
                // FacultySignUpPage still a Window — open separately
                var facultySignUp = new FacultySignUpPage();
                facultySignUp.Show();
            }
        }
    }
}

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
        private Models.User? _pendingInstructorUser;

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
                var (user, errorMessage) = await _authService.LoginWithStatusAsync(username, password);

                if (user == null)
                {
                    MessageBox.Show(errorMessage ?? "Invalid username or password. Please try again.",
                        errorMessage?.Contains("deactivated", StringComparison.OrdinalIgnoreCase) == true ? "Account Deactivated" : "Login Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                SessionManager.CurrentUser = user;

                using var context = new AppDbContext();
                if (user.Role == "student")
                {
                    var student = await context.Students
                        .FirstOrDefaultAsync(s => s.UserId == user.UserId);

                    if (student == null || !student.IsActive)
                    {
                        MessageBox.Show("This account has been deactivated. Contact your department administrator.",
                            "Account Deactivated", MessageBoxButton.OK, MessageBoxImage.Warning);
                        SessionManager.CurrentUser = null;
                        return;
                    }

                    if (student.MustChangePassword)
                    {
                        var dialog = new ChangePasswordDialog(student.StudentId)
                        {
                            Owner = Window.GetWindow(this)
                        };
                        var result = dialog.ShowDialog();

                        if (result == true)
                        {
                            string newPassword = dialog.EnteredPassword;
                            if (!string.IsNullOrEmpty(newPassword))
                            {
                                string newHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);
                                user.PasswordHash = newHash;
                                if (student.User != null) student.User.PasswordHash = newHash;
                            }
                            student.MustChangePassword = false;
                            await context.SaveChangesAsync();
                        }
                        else
                        {
                            // Student cancelled; terminate login sequence
                            MessageBox.Show("Password update required to access account.", "Password Update Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                            SessionManager.CurrentUser = null;
                            SessionManager.CurrentStudent = null;
                            return;
                        }
                    }

                    SessionManager.CurrentStudent = student;
                    SessionManager.RefreshAvatarCache();

                    ACPAMS.MainWindow.Resize(520, 480);
                    ACPAMS.MainWindow.Navigate(new Student.ExamEntryPage());
                }
                else if (user.Role == "instructor")
                {
                    SessionManager.CurrentInstructor = await context.Instructors
                        .FirstOrDefaultAsync(i => i.UserId == user.UserId);
                    SessionManager.RefreshAvatarCache();

                    if (AppInitializer.IsDefaultPassword(user.PasswordHash) || password == AppInitializer.DefaultPassword)
                    {
                        _pendingInstructorUser = user;
                        NewPasswordBox.Password = string.Empty;
                        ConfirmPasswordBox.Password = string.Empty;
                        ChangePasswordErrorTxt.Text = string.Empty;
                        ChangePasswordErrorTxt.Visibility = Visibility.Collapsed;
                        ChangePasswordOverlay.Visibility = Visibility.Visible;
                        return;
                    }

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

        private void CancelChangePasswordBtn_Click(object sender, RoutedEventArgs e)
        {
            _pendingInstructorUser = null;
            SessionManager.CurrentUser = null;
            SessionManager.CurrentInstructor = null;
            ChangePasswordOverlay.Visibility = Visibility.Collapsed;
            PasswordBox.Password = string.Empty;
            PasswordVisibleBox.Text = string.Empty;
        }

        private async void SavePasswordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingInstructorUser == null) return;

            string newPass = NewPasswordBox.Password.Trim();
            string confirmPass = ConfirmPasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(newPass))
            {
                ChangePasswordErrorTxt.Text = "New password cannot be empty.";
                ChangePasswordErrorTxt.Visibility = Visibility.Visible;
                return;
            }

            if (newPass.Length < 6)
            {
                ChangePasswordErrorTxt.Text = "Password must be at least 6 characters long.";
                ChangePasswordErrorTxt.Visibility = Visibility.Visible;
                return;
            }

            if (newPass == AppInitializer.DefaultPassword)
            {
                ChangePasswordErrorTxt.Text = "Password cannot be the default password.";
                ChangePasswordErrorTxt.Visibility = Visibility.Visible;
                return;
            }

            if (newPass != confirmPass)
            {
                ChangePasswordErrorTxt.Text = "Passwords do not match.";
                ChangePasswordErrorTxt.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                SavePasswordBtn.IsEnabled = false;
                SavePasswordBtn.Content = "Saving...";

                using var context = new AppDbContext();
                var dbUser = await context.Users.FirstOrDefaultAsync(u => u.UserId == _pendingInstructorUser.UserId);
                if (dbUser != null)
                {
                    string newHash = BCrypt.Net.BCrypt.HashPassword(newPass);
                    dbUser.PasswordHash = newHash;
                    await context.SaveChangesAsync();

                    _pendingInstructorUser.PasswordHash = newHash;
                    SessionManager.CurrentUser = _pendingInstructorUser;
                }

                ChangePasswordOverlay.Visibility = Visibility.Collapsed;
                _pendingInstructorUser = null;

                ACPAMS.MainWindow.Resize(1280, 800);
                ACPAMS.MainWindow.Navigate(new FacultyHomePage());
            }
            catch (Exception ex)
            {
                ChangePasswordErrorTxt.Text = $"Failed to update password: {ex.Message}";
                ChangePasswordErrorTxt.Visibility = Visibility.Visible;
            }
            finally
            {
                SavePasswordBtn.IsEnabled = true;
                SavePasswordBtn.Content = "Save & Continue";
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
            var dialog = new ForgotPasswordDialog
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
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
                ACPAMS.MainWindow.Resize(520, 720);
                ACPAMS.MainWindow.Navigate(new FacultySignUpPage());
            }
        }

        private void OpenSetupWizard_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new SetupWizardWindow
            {
                Owner = Window.GetWindow(this)
            };
            wizard.ShowDialog();
        }
    }
}

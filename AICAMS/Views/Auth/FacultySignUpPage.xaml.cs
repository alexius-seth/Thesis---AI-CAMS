using ACPAMS.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Auth
{
    public partial class FacultySignUpPage : UserControl
    {
        private readonly AuthService _authService;
        private bool _passwordVisible = false;

        public FacultySignUpPage()
        {
            InitializeComponent();
            _authService = new AuthService();
            ACPAMS.MainWindow.Resize(520, 720);
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorCard.Visibility = Visibility.Collapsed;
            ErrorMessageText.Text = string.Empty;

            string firstName = FirstNameBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();
            string middleInitial = MiddleInitialBox.Text.Trim();
            string username = UsernameBox.Text.Trim();
            string registrationCode = RegistrationCodeBox.Text.Trim();
            string password = _passwordVisible
                ? PasswordVisibleBox.Text
                : PasswordBox.Password;

            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(registrationCode) ||
                string.IsNullOrEmpty(password))
            {
                ShowError("Please fill in all required fields.");
                return;
            }

            SignUpBtn.IsEnabled = false;
            SignUpBtn.Content = "Creating account...";

            try
            {
                var (success, errorMessage) = await _authService.RegisterFacultyAsync(
                    firstName, middleInitial, lastName, username, password, registrationCode);

                if (!success)
                {
                    ShowError(errorMessage ?? "Registration failed.");
                    return;
                }

                MessageBox.Show("Faculty account created successfully! You can now log in.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ACPAMS.MainWindow.Resize(520, 600);
                ACPAMS.MainWindow.Navigate(new LoginPage());
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred: {ex.Message}");
            }
            finally
            {
                SignUpBtn.IsEnabled = true;
                SignUpBtn.Content = "Sign Up";
            }
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorCard.Visibility = Visibility.Visible;
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

        private void LogInBtn_Click(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Resize(520, 600);
            ACPAMS.MainWindow.Navigate(new LoginPage());
        }
    }
}

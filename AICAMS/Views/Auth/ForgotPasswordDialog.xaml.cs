using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;

namespace ACPAMS.Views.Auth
{
    public partial class ForgotPasswordDialog : Window
    {
        public ForgotPasswordDialog()
        {
            InitializeComponent();
        }

        private void RoleTab_Checked(object sender, RoutedEventArgs e)
        {
            if (StudentRecoveryPanel == null || FacultyRecoveryPanel == null || SubmitResetBtn == null)
                return;

            if (StudentTabRadio.IsChecked == true)
            {
                StudentRecoveryPanel.Visibility = Visibility.Visible;
                FacultyRecoveryPanel.Visibility = Visibility.Collapsed;
                SubmitResetBtn.Visibility = Visibility.Collapsed;
                CancelBtn.Content = "Close";
                ErrorCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                StudentRecoveryPanel.Visibility = Visibility.Collapsed;
                FacultyRecoveryPanel.Visibility = Visibility.Visible;
                SubmitResetBtn.Visibility = Visibility.Visible;
                CancelBtn.Content = "Cancel";
                ErrorCard.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SubmitResetBtn_Click(object sender, RoutedEventArgs e)
        {
            ErrorCard.Visibility = Visibility.Collapsed;

            string masterKey = MasterKeyBox.Password;
            string expectedKey = App.Configuration?["Security:MasterRecoveryKey"] ?? "AICAMS-ADMIN-2026";

            if (string.IsNullOrWhiteSpace(masterKey) || masterKey != expectedKey)
            {
                ShowError("Invalid master recovery key. Access denied.");
                return;
            }

            string newPass = NewPasswordBox.Password;
            string confirmPass = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < 6)
            {
                ShowError("New password must be at least 6 characters long.");
                return;
            }

            if (newPass != confirmPass)
            {
                ShowError("New password and confirmation do not match.");
                return;
            }

            string enteredUsername = FacultyUsernameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(enteredUsername))
            {
                ShowError("Please enter your faculty username.");
                return;
            }

            try
            {
                SubmitResetBtn.IsEnabled = false;
                SubmitResetBtn.Content = "Updating...";

                using var context = new AppDbContext();
                var instructorUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == enteredUsername && (u.Role == "Instructor" || u.Role == "Admin" || u.Role == "instructor" || u.Role == "admin"));

                if (instructorUser == null)
                {
                    MessageBox.Show("Faculty account not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowError("Faculty account not found.");
                    return;
                }

                instructorUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass, 12);
                await context.SaveChangesAsync();

                MessageBox.Show(
                    "Faculty credentials successfully updated. You may now log in.",
                    "Password Recovery",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Database error: {ex.Message}");
            }
            finally
            {
                SubmitResetBtn.IsEnabled = true;
                SubmitResetBtn.Content = "Update Password";
            }
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorCard.Visibility = Visibility.Visible;
        }
    }
}


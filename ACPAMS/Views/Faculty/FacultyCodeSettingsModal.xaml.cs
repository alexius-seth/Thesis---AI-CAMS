using ACPAMS.Services;
using System;
using System.Windows;

namespace ACPAMS.Views.Faculty
{
    public partial class FacultyCodeSettingsModal : Window
    {
        public FacultyCodeSettingsModal()
        {
            InitializeComponent();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            ErrorCard.Visibility = Visibility.Collapsed;
            ErrorMessageText.Text = string.Empty;

            string currentCode = CurrentCodeBox.Text.Trim();
            string newCode = NewCodeBox.Text.Trim();
            string confirmCode = ConfirmCodeBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(currentCode))
            {
                ShowError("Please enter the current registration code.");
                return;
            }

            if (!AuthService.VerifyFacultyRegistrationCode(currentCode))
            {
                ShowError("Current registration code is incorrect.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newCode))
            {
                ShowError("Please enter a new registration code.");
                return;
            }

            if (newCode.Length < 6)
            {
                ShowError("New registration code must be at least 6 characters.");
                return;
            }

            if (!string.Equals(newCode, confirmCode, StringComparison.Ordinal))
            {
                ShowError("Registration codes do not match.");
                return;
            }

            if (string.Equals(currentCode, newCode, StringComparison.Ordinal))
            {
                ShowError("New code must be different from current code.");
                return;
            }

            SaveBtn.IsEnabled = false;
            SaveBtn.Content = "Saving...";

            try
            {
                AuthService.UpdateFacultyRegistrationCode(newCode);

                MessageBox.Show(
                    "Faculty registration code has been updated successfully.",
                    "Settings Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to update code: {ex.Message}");
            }
            finally
            {
                SaveBtn.IsEnabled = true;
                SaveBtn.Content = "Save Code";
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorCard.Visibility = Visibility.Visible;
        }
    }
}

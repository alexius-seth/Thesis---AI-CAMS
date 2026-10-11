using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;

namespace ACPAMS.Views.Auth
{
    public partial class ChangePasswordDialog : Window
    {
        private readonly string? _studentId;

        public string EnteredPassword { get; private set; } = string.Empty;

        public ChangePasswordDialog() : this(null)
        {
        }

        public ChangePasswordDialog(string? studentId)
        {
            _studentId = studentId;
            InitializeComponent();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            ErrorCard.Visibility = Visibility.Collapsed;

            string newPass = NewPasswordBox.Password.Trim();
            string confirmPass = ConfirmPasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(newPass) || string.IsNullOrWhiteSpace(confirmPass))
            {
                ShowError("Please fill in both password fields.");
                return;
            }

            if (newPass.Length < 6)
            {
                ShowError("Password must be at least 6 characters long.");
                return;
            }

            if (!string.IsNullOrEmpty(_studentId) && newPass.Equals($"Temp@{_studentId}", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("New password cannot match your temporary reset password.");
                return;
            }

            if (newPass.StartsWith("Temp@", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("New password cannot be a temporary reset password.");
                return;
            }

            if (newPass != confirmPass)
            {
                ShowError("Passwords do not match.");
                return;
            }

            try
            {
                ConfirmBtn.IsEnabled = false;
                ConfirmBtn.Content = "Updating...";

                EnteredPassword = newPass;

                if (!string.IsNullOrWhiteSpace(_studentId))
                {
                    using var context = new AppDbContext();
                    var student = await context.Students
                        .Include(s => s.User)
                        .FirstOrDefaultAsync(s => s.StudentId == _studentId);

                    if (student != null)
                    {
                        string hash = BCrypt.Net.BCrypt.HashPassword(newPass, 12);
                        student.MustChangePassword = false;

                        if (student.User != null)
                        {
                            student.User.PasswordHash = hash;
                        }
                        else
                        {
                            var user = await context.Users.FirstOrDefaultAsync(u => u.Username == student.StudentId);
                            if (user != null)
                            {
                                user.PasswordHash = hash;
                            }
                        }

                        await context.SaveChangesAsync();
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to update password: {ex.Message}");
            }
            finally
            {
                ConfirmBtn.IsEnabled = true;
                ConfirmBtn.Content = "Confirm";
            }
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorCard.Visibility = Visibility.Visible;
        }
    }
}


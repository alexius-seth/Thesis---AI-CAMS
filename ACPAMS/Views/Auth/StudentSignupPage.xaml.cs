using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using StudentModel = ACPAMS.Models.Student;

namespace ACPAMS.Views.Auth
{
    public partial class StudentSignUpPage : UserControl
    {
        private readonly AuthService _authService;
        private bool _passwordVisible = false;

        public StudentSignUpPage()
        {
            InitializeComponent();
            _authService = new AuthService();
            ACPAMS.MainWindow.Resize(520, 740);
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            string firstName = FirstNameBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();
            string middleInitial = MiddleInitialBox.Text.Trim();
            string studentId = StudentIDBox.Text.Trim();
            string password = _passwordVisible
                ? PasswordVisibleBox.Text
                : PasswordBox.Password;

            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please fill in all required fields.",
                    "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string program = (ProgramBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
            string yearLevel = (YearLevelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(program))
            {
                MessageBox.Show("Please select your program.",
                    "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(yearLevel))
            {
                MessageBox.Show("Please select your year level.",
                    "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_authService.ValidatePassword(password))
            {
                MessageBox.Show(
                    "Password must be at least 8 characters and include letters, numbers, and symbols.",
                    "Weak Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SignUpBtn.IsEnabled = false;
            SignUpBtn.Content = "Creating account...";

            try
            {
                using var context = new AppDbContext();

                bool exists = context.Students.Any(s => s.StudentId == studentId);
                if (exists)
                {
                    MessageBox.Show("A student with this ID already exists.",
                        "Duplicate ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var user = new User
                {
                    UserId = Guid.NewGuid(),
                    Role = "student",
                    Username = studentId,
                    PasswordHash = _authService.HashPassword(password),
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var student = new StudentModel
                {
                    StudentId = studentId,
                    UserId = user.UserId,
                    FirstName = firstName,
                    LastName = lastName,
                    MiddleInitial = string.IsNullOrEmpty(middleInitial) ? null : middleInitial,
                    Program = program,
                    YearLevel = yearLevel,
                    CreatedAt = DateTime.Now
                };

                context.Users.Add(user);
                context.Students.Add(student);
                await context.SaveChangesAsync();

                MessageBox.Show("Account created successfully! You can now log in.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ACPAMS.MainWindow.Resize(520, 600);
                ACPAMS.MainWindow.Navigate(new LoginPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SignUpBtn.IsEnabled = true;
                SignUpBtn.Content = "Sign Up";
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

        private void LogInBtn_Click(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Resize(520, 600);
            ACPAMS.MainWindow.Navigate(new LoginPage());
        }
    }
}

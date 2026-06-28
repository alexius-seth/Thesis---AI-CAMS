using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using System.Windows;
using StudentModel = ACPAMS.Models.Student;
namespace ACPAMS.Views.Auth
{
    public partial class StudentSignUpPage : Window
    {
        private readonly AuthService _authService;
        private bool _passwordVisible = false;

        public StudentSignUpPage()
        {
            InitializeComponent();
            _authService = new AuthService();
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            string firstName = FirstNameBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();
            string middleInitial = MiddleInitialBox.Text.Trim();
            string studentId = StudentIDBox.Text.Trim();
            string password = PasswordBox.Password;

            // Validate fields
            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please fill in all required fields.",
                    "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate password strength
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

                // Check if student ID already exists
                bool exists = context.Students.Any(s => s.StudentId == studentId);
                if (exists)
                {
                    MessageBox.Show("A student with this ID already exists.",
                        "Duplicate ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create user
                var user = new User
                {
                    UserId = Guid.NewGuid(),
                    Role = "student",
                    Username = studentId,
                    PasswordHash = _authService.HashPassword(password),
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                // Create student profileS
                var student = new StudentModel
                {
                    StudentId = studentId,
                    UserId = user.UserId,
                    FirstName = firstName,
                    LastName = lastName,
                    MiddleInitial = string.IsNullOrEmpty(middleInitial) ? null : middleInitial,
                    CreatedAt = DateTime.Now
                };

                context.Users.Add(user);
                context.Students.Add(student);
                await context.SaveChangesAsync();

                MessageBox.Show("Account created successfully! You can now log in.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Go back to login
                var login = new LoginPage();
                login.Show();
                this.Close();
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
            var login = new LoginPage();
            login.Show();
            this.Close();
        }
    }
}
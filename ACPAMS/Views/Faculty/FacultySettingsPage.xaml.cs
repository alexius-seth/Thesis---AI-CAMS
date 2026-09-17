using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty
{
    public partial class FacultySettingsPage : UserControl, IRefreshableView
    {
        public FacultySettingsPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            await LoadProfileAsync();
            LoadGeminiModel();
            LoadGradingThresholds();
            LoadMonitoringThresholds();
        }

        // ─── Section 1: Profile ─────────────────────────────────────────────

        private async Task LoadProfileAsync()
        {
            try
            {
                var currentUser = SessionManager.CurrentUser;
                if (currentUser == null) return;

                using var db = new AppDbContext();
                var user = await db.Users
                    .Include(u => u.Instructor)
                    .FirstOrDefaultAsync(u => u.UserId == currentUser.UserId);

                if (user != null)
                {
                    SessionManager.CurrentUser = user;
                    if (user.Instructor != null)
                    {
                        SessionManager.CurrentInstructor = user.Instructor;
                    }
                }

                var instructor = SessionManager.CurrentInstructor;
                if (instructor != null)
                {
                    FirstNameBox.Text = instructor.FirstName ?? string.Empty;
                    LastNameBox.Text = instructor.LastName ?? string.Empty;
                    MiddleInitialBox.Text = instructor.MiddleInitial ?? string.Empty;
                    DepartmentBox.Text = instructor.Department ?? string.Empty;
                }

                UsernameBox.Text = SessionManager.CurrentUser?.Username ?? string.Empty;
            }
            catch (Exception ex)
            {
                ShowAlert(ProfileAlertBanner, ProfileAlertText, $"Error loading profile: {ex.Message}", false);
            }
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string firstName = FirstNameBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();
            string middleInitial = MiddleInitialBox.Text.Trim();
            string department = DepartmentBox.Text.Trim();
            string username = UsernameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                ShowAlert(ProfileAlertBanner, ProfileAlertText, "First and last names are required.", false);
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowAlert(ProfileAlertBanner, ProfileAlertText, "Username is required.", false);
                return;
            }

            string currentPassword = CurrentPasswordBox.Password;
            string newPassword = NewPasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;
            bool isChangingPassword = !string.IsNullOrEmpty(currentPassword) || !string.IsNullOrEmpty(newPassword) || !string.IsNullOrEmpty(confirmPassword);

            try
            {
                var currentUser = SessionManager.CurrentUser;
                if (currentUser == null)
                {
                    ShowAlert(ProfileAlertBanner, ProfileAlertText, "No active session found.", false);
                    return;
                }

                using var db = new AppDbContext();
                var user = await db.Users
                    .Include(u => u.Instructor)
                    .FirstOrDefaultAsync(u => u.UserId == currentUser.UserId);

                if (user == null)
                {
                    ShowAlert(ProfileAlertBanner, ProfileAlertText, "User record not found.", false);
                    return;
                }

                // Check username uniqueness if changed
                if (!string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    bool taken = await db.Users.AnyAsync(u => u.Username == username && u.UserId != user.UserId);
                    if (taken)
                    {
                        ShowAlert(ProfileAlertBanner, ProfileAlertText, "Username is already in use by another account.", false);
                        return;
                    }
                    user.Username = username;
                }

                // Validate password change
                if (isChangingPassword)
                {
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        ShowAlert(ProfileAlertBanner, ProfileAlertText, "Current password is required to change password.", false);
                        return;
                    }

                    if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                    {
                        ShowAlert(ProfileAlertBanner, ProfileAlertText, "Current password is incorrect.", false);
                        return;
                    }

                    if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
                    {
                        ShowAlert(ProfileAlertBanner, ProfileAlertText, "New password must be at least 6 characters.", false);
                        return;
                    }

                    if (newPassword != confirmPassword)
                    {
                        ShowAlert(ProfileAlertBanner, ProfileAlertText, "New password and confirmation do not match.", false);
                        return;
                    }

                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                }

                // Update instructor record
                if (user.Instructor != null)
                {
                    user.Instructor.FirstName = firstName;
                    user.Instructor.LastName = lastName;
                    user.Instructor.MiddleInitial = string.IsNullOrWhiteSpace(middleInitial) ? null : middleInitial;
                    user.Instructor.Department = string.IsNullOrWhiteSpace(department) ? null : department;
                }

                await db.SaveChangesAsync();

                // Update session state
                SessionManager.CurrentUser = user;
                if (user.Instructor != null)
                {
                    SessionManager.CurrentInstructor = user.Instructor;
                }

                CurrentPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmPasswordBox.Clear();

                ShowAlert(ProfileAlertBanner, ProfileAlertText, "Profile updated successfully.", true);
            }
            catch (Exception ex)
            {
                ShowAlert(ProfileAlertBanner, ProfileAlertText, $"Error saving profile: {ex.Message}", false);
            }
        }

        // ─── Section 2: Gemini Model & API Key ─────────────────────────────

        private bool _isKeyRevealed = false;

        private void LoadGeminiModel()
        {
            GeminiModelBox.Text = AppSettingsService.GetGeminiModel();
            string key = AppSettingsService.GetGeminiApiKey();
            if (!string.IsNullOrEmpty(key))
            {
                GeminiApiKeyBox.Password = key;
                GeminiApiKeyPlainBox.Text = key;
            }
        }

        private void ToggleApiKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isKeyRevealed = !_isKeyRevealed;
            if (_isKeyRevealed)
            {
                GeminiApiKeyPlainBox.Text = GeminiApiKeyBox.Password;
                GeminiApiKeyBox.Visibility = Visibility.Collapsed;
                GeminiApiKeyPlainBox.Visibility = Visibility.Visible;
                ToggleApiKeyVisibilityBtn.Content = "🙈 Hide";
            }
            else
            {
                GeminiApiKeyBox.Password = GeminiApiKeyPlainBox.Text;
                GeminiApiKeyPlainBox.Visibility = Visibility.Collapsed;
                GeminiApiKeyBox.Visibility = Visibility.Visible;
                ToggleApiKeyVisibilityBtn.Content = "👁 Show";
            }
        }

        private async void TestGemini_Click(object sender, RoutedEventArgs e)
        {
            string key = _isKeyRevealed ? GeminiApiKeyPlainBox.Text.Trim() : GeminiApiKeyBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(key))
                key = AppSettingsService.GetGeminiApiKey();

            string model = GeminiModelBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                ShowAlert(GeminiAlertBanner, GeminiAlertText, "Please enter an API key to test the connection.", false);
                return;
            }

            TestGeminiBtn.IsEnabled = false;
            TestGeminiBtn.Content = "Testing...";

            try
            {
                var (success, message) = await AppSettingsService.TestGeminiConnectionAsync(key, model);
                ShowAlert(GeminiAlertBanner, GeminiAlertText, message, success);
            }
            finally
            {
                TestGeminiBtn.IsEnabled = true;
                TestGeminiBtn.Content = "Test Connection";
            }
        }

        private async void SaveModel_Click(object sender, RoutedEventArgs e)
        {
            string model = GeminiModelBox.Text?.Trim() ?? string.Empty;
            string key = _isKeyRevealed ? GeminiApiKeyPlainBox.Text.Trim() : GeminiApiKeyBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(model))
            {
                ShowAlert(GeminiAlertBanner, GeminiAlertText, "Model name cannot be empty.", false);
                return;
            }

            try
            {
                AppSettingsService.SaveGeminiModel(model);

                if (!string.IsNullOrWhiteSpace(key))
                {
                    AppSettingsService.SaveGeminiApiKey(key);
                }

                var currentUser = SessionManager.CurrentUser;
                if (currentUser != null)
                {
                    using var db = new AppDbContext();
                    db.AuditLogs.Add(new AuditLog
                    {
                        UserId = currentUser.UserId,
                        Action = "UpdateGeminiConfig",
                        EntityType = "SystemConfiguration",
                        Details = $"Status: Success. Active Gemini model updated to '{model}'.",
                        CreatedAt = DateTime.Now
                    });
                    await db.SaveChangesAsync();
                }

                ShowAlert(GeminiAlertBanner, GeminiAlertText, $"AI settings saved successfully (Model: {model}).", true);
            }
            catch (Exception ex)
            {
                ShowAlert(GeminiAlertBanner, GeminiAlertText, $"Error saving AI settings: {ex.Message}", false);
            }
        }

        // ─── Section 3: Grading Cutoffs ─────────────────────────────────────

        private void LoadGradingThresholds()
        {
            PassingGradeBox.Text = AppSettingsService.PassingGrade.ToString("0.##");
            ExcellentBox.Text = AppSettingsService.ExcellentThreshold.ToString("0.##");
            GoodBox.Text = AppSettingsService.GoodThreshold.ToString("0.##");
            FairBox.Text = AppSettingsService.FairThreshold.ToString("0.##");
            FlagCapBox.Text = AppSettingsService.IntegrityFlagCap.ToString();
        }

        private void SaveGrading_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(PassingGradeBox.Text, out decimal passing) ||
                !decimal.TryParse(ExcellentBox.Text, out decimal excellent) ||
                !decimal.TryParse(GoodBox.Text, out decimal good) ||
                !decimal.TryParse(FairBox.Text, out decimal fair) ||
                !int.TryParse(FlagCapBox.Text, out int flagCap))
            {
                ShowAlert(GradingAlertBanner, GradingAlertText, "Please enter valid numeric values for all fields.", false);
                return;
            }

            try
            {
                AppSettingsService.SaveGradingCutoffs(passing, excellent, good, fair, flagCap);
                ShowAlert(GradingAlertBanner, GradingAlertText, "Grading cutoffs and thresholds saved successfully.", true);
            }
            catch (Exception ex)
            {
                ShowAlert(GradingAlertBanner, GradingAlertText, ex.Message, false);
            }
        }

        // ─── Section 4: Monitoring Sensitivity ──────────────────────────────

        private void LoadMonitoringThresholds()
        {
            YawThresholdBox.Text = AppSettingsService.HeadYawThreshold.ToString("0.##");
            PitchThresholdBox.Text = AppSettingsService.HeadPitchThreshold.ToString("0.##");
            NoFaceFramesBox.Text = AppSettingsService.NoFaceFrameThreshold.ToString();
            WiggleFramesBox.Text = AppSettingsService.WiggleRoomFrames.ToString();
            AlertCooldownBox.Text = AppSettingsService.AlertCooldownSeconds.ToString();
            DecayRateBox.Text = AppSettingsService.ScoreDecayRate.ToString("0.###");
        }

        private void SaveMonitoring_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(YawThresholdBox.Text, out double yaw) ||
                !double.TryParse(PitchThresholdBox.Text, out double pitch) ||
                !int.TryParse(NoFaceFramesBox.Text, out int noFace) ||
                !int.TryParse(WiggleFramesBox.Text, out int wiggle) ||
                !int.TryParse(AlertCooldownBox.Text, out int cooldown) ||
                !double.TryParse(DecayRateBox.Text, out double decay))
            {
                ShowAlert(MonitoringAlertBanner, MonitoringAlertText, "Please enter valid numeric values for all fields.", false);
                return;
            }

            try
            {
                AppSettingsService.SaveMonitoringSettings(yaw, pitch, noFace, wiggle, cooldown, decay);
                ShowAlert(MonitoringAlertBanner, MonitoringAlertText, "Monitoring sensitivity thresholds saved successfully.", true);
            }
            catch (Exception ex)
            {
                ShowAlert(MonitoringAlertBanner, MonitoringAlertText, ex.Message, false);
            }
        }

        private void ResetMonitoringDefaults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppSettingsService.ResetMonitoringDefaults();
                LoadMonitoringThresholds();
                ShowAlert(MonitoringAlertBanner, MonitoringAlertText, "Monitoring sensitivity reset to standard defaults.", true);
            }
            catch (Exception ex)
            {
                ShowAlert(MonitoringAlertBanner, MonitoringAlertText, ex.Message, false);
            }
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        private void ShowAlert(Border banner, TextBlock textBlock, string message, bool isSuccess)
        {
            banner.Background = isSuccess
                ? (SolidColorBrush)FindResource("Status.SuccessBg")
                : (SolidColorBrush)FindResource("Status.DangerBg");
            banner.BorderBrush = isSuccess
                ? (SolidColorBrush)FindResource("Status.Success")
                : (SolidColorBrush)FindResource("Status.Danger");
            banner.BorderThickness = new Thickness(1);

            textBlock.Foreground = isSuccess
                ? (SolidColorBrush)FindResource("Status.Success")
                : (SolidColorBrush)FindResource("Status.Danger");
            textBlock.Text = message;
            banner.Visibility = Visibility.Visible;
        }

        // ─── Navigation ─────────────────────────────────────────────────────

        private void NavigateToHome(object sender, RoutedEventArgs e) => MainWindow.NavigateToHomeStatic();
        private void NavigateToDashboard(object sender, RoutedEventArgs e) => MainWindow.NavigateToDashboardStatic();
        private void NavigateToCreateExam(object sender, RoutedEventArgs e) => MainWindow.NavigateToCreateExamStatic();
        private void NavigateToArchivedExams(object sender, RoutedEventArgs e) => MainWindow.NavigateToArchivedExamsStatic();

        private void OpenRegistrationCodeSettings_Click(object sender, RoutedEventArgs e)
        {
            var modal = new FacultyCodeSettingsModal
            {
                Owner = Window.GetWindow(this)
            };
            modal.ShowDialog();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Clear();
            MainWindow.Resize(520, 600);
            MainWindow.Navigate(new Views.Auth.LoginPage());
        }
    }
}

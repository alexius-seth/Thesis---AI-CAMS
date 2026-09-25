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
                }

                UsernameBox.Text = SessionManager.CurrentUser?.Username ?? string.Empty;
                UpdateAvatarDisplay();
            }
            catch (Exception ex)
            {
                ShowAlert(ProfileAlertBanner, ProfileAlertText, $"Error loading profile: {ex.Message}", false);
            }
        }

        private void UpdateAvatarDisplay()
        {
            if (SessionManager.CachedAvatar != null)
            {
                SettingsAvatarBrush.ImageSource = SessionManager.CachedAvatar;
                SettingsAvatarImageBorder.Visibility = Visibility.Visible;
                SettingsInitialsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                SettingsAvatarImageBorder.Visibility = Visibility.Collapsed;
                SettingsInitialsText.Text = SessionManager.Initials;
                SettingsInitialsText.Visibility = Visibility.Visible;
            }
        }

        private async void UploadAvatar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Select Profile Photo"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    byte[] imageBytes = AvatarService.ProcessAndCropToSquare(dlg.FileName);

                    using var db = new AppDbContext();
                    var instructorId = SessionManager.CurrentInstructor?.InstructorId;
                    if (string.IsNullOrEmpty(instructorId)) return;

                    var instructor = await db.Instructors.FirstOrDefaultAsync(i => i.InstructorId == instructorId);
                    if (instructor != null)
                    {
                        instructor.AvatarImage = imageBytes;
                        await db.SaveChangesAsync();

                        SessionManager.CurrentInstructor.AvatarImage = imageBytes;
                        SessionManager.RefreshAvatarCache();
                        UpdateAvatarDisplay();

                        ShowAlert(ProfileAlertBanner, ProfileAlertText, "Profile photo updated successfully!", true);
                    }
                }
                catch (Exception ex)
                {
                    ShowAlert(ProfileAlertBanner, ProfileAlertText, $"Failed to upload photo: {ex.Message}", false);
                }
            }
        }

        private async void RemoveAvatar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                var instructorId = SessionManager.CurrentInstructor?.InstructorId;
                if (string.IsNullOrEmpty(instructorId)) return;

                var instructor = await db.Instructors.FirstOrDefaultAsync(i => i.InstructorId == instructorId);
                if (instructor != null)
                {
                    instructor.AvatarImage = null;
                    await db.SaveChangesAsync();

                    SessionManager.CurrentInstructor.AvatarImage = null;
                    SessionManager.RefreshAvatarCache();
                    UpdateAvatarDisplay();

                    ShowAlert(ProfileAlertBanner, ProfileAlertText, "Profile photo removed.", true);
                }
            }
            catch (Exception ex)
            {
                ShowAlert(ProfileAlertBanner, ProfileAlertText, $"Failed to remove photo: {ex.Message}", false);
            }
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string firstName = FirstNameBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();
            string middleInitial = MiddleInitialBox.Text.Trim();
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
                    user.Instructor.Department = "Department of Computer Studies";
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

        private const string MASKED_KEY_PLACEHOLDER = "••••••••••••••••••••••••••••••••";

        private void LoadGeminiModel()
        {
            var activeModel = AppSettingsService.GetGeminiModel();
            string target = !string.IsNullOrWhiteSpace(activeModel) ? activeModel : "gemini-3.5-flash";
            GeminiModelBox.Text = target;
            foreach (var item in GeminiModelBox.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Content?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    GeminiModelBox.SelectedItem = cbi;
                    break;
                }
            }
            string key = AppSettingsService.GetGeminiApiKey();
            GeminiApiKeyBox.Password = !string.IsNullOrEmpty(key) ? MASKED_KEY_PLACEHOLDER : string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
            {
                _ = PopulateDiscoveredModelsAsync(key, target);
            }
        }

        private async Task PopulateDiscoveredModelsAsync(string key, string? preferredModel = null)
        {
            if (string.IsNullOrWhiteSpace(key) || key == MASKED_KEY_PLACEHOLDER)
            {
                key = AppSettingsService.GetGeminiApiKey();
            }

            if (string.IsNullOrWhiteSpace(key)) return;

            try
            {
                var available = await AppSettingsService.GetAvailableModelsAsync(key);
                if (available != null && available.Count > 0)
                {
                    string target = !string.IsNullOrWhiteSpace(preferredModel)
                        ? preferredModel
                        : GetSelectedGeminiModel();

                    GeminiModelBox.Items.Clear();
                    ComboBoxItem? matchedItem = null;

                    foreach (var m in available)
                    {
                        var cbi = new ComboBoxItem { Content = m };
                        GeminiModelBox.Items.Add(cbi);
                        if (matchedItem == null && string.Equals(m, target, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedItem = cbi;
                        }
                    }

                    if (matchedItem != null)
                    {
                        GeminiModelBox.SelectedItem = matchedItem;
                        GeminiModelBox.Text = matchedItem.Content.ToString();
                    }
                    else if (GeminiModelBox.Items.Count > 0)
                    {
                        GeminiModelBox.SelectedIndex = 0;
                        if (GeminiModelBox.SelectedItem is ComboBoxItem first && first.Content != null)
                            GeminiModelBox.Text = first.Content.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Model discovery error: {ex.Message}");
            }
        }

        private string GetSelectedGeminiModel()
        {
            if (GeminiModelBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null)
                return cbi.Content.ToString()!.Trim();
            if (GeminiModelBox.SelectedValue is string sv && !string.IsNullOrWhiteSpace(sv))
                return sv.Trim();
            if (GeminiModelBox.SelectedItem is string str && !string.IsNullOrWhiteSpace(str))
                return str.Trim();
            return GeminiModelBox.Text?.Trim() ?? string.Empty;
        }

        private void GeminiApiKeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (GeminiApiKeyBox.Password == MASKED_KEY_PLACEHOLDER)
            {
                GeminiApiKeyBox.SelectAll();
            }
        }

        private void GeminiApiKeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(GeminiApiKeyBox.Password) && !string.IsNullOrEmpty(AppSettingsService.GetGeminiApiKey()))
            {
                GeminiApiKeyBox.Password = MASKED_KEY_PLACEHOLDER;
            }
            else if (!string.IsNullOrWhiteSpace(GeminiApiKeyBox.Password) && GeminiApiKeyBox.Password != MASKED_KEY_PLACEHOLDER)
            {
                _ = PopulateDiscoveredModelsAsync(GeminiApiKeyBox.Password.Trim());
            }
        }

        private async void TestGemini_Click(object sender, RoutedEventArgs e)
        {
            string key = GeminiApiKeyBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(key) || key == MASKED_KEY_PLACEHOLDER)
            {
                key = AppSettingsService.GetGeminiApiKey();
            }

            string model = GetSelectedGeminiModel();
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

                // Populate and sync available models on test
                await PopulateDiscoveredModelsAsync(key, model);
            }
            finally
            {
                TestGeminiBtn.IsEnabled = true;
                TestGeminiBtn.Content = "Test Connection";
            }
        }

        private async void SaveModel_Click(object sender, RoutedEventArgs e)
        {
            string model = GetSelectedGeminiModel();
            string key = GeminiApiKeyBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(model))
            {
                ShowAlert(GeminiAlertBanner, GeminiAlertText, "Model name cannot be empty.", false);
                return;
            }

            try
            {
                AppSettingsService.SaveGeminiModel(model);
                GeminiModelBox.Text = AppSettingsService.GetGeminiModel();

                // Preserve existing key if placeholder is untouched; update if a new key was pasted/typed
                if (!string.IsNullOrWhiteSpace(key) && key != MASKED_KEY_PLACEHOLDER)
                {
                    AppSettingsService.SaveGeminiApiKey(key);
                    GeminiApiKeyBox.Password = MASKED_KEY_PLACEHOLDER;
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
                        Details = $"Status: Success. Active Gemini model updated to '{AppSettingsService.GetGeminiModel()}'.",
                        CreatedAt = DateTime.Now
                    });
                    await db.SaveChangesAsync();
                }

                ShowAlert(GeminiAlertBanner, GeminiAlertText, $"AI settings saved successfully (Model: {AppSettingsService.GetGeminiModel()}).", true);
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
            SessionManager.Logout();
        }
    }
}

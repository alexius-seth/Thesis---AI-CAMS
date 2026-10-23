using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Views.Faculty
{
    public class StudentSuggestionDto
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string DisplaySubtext => string.IsNullOrWhiteSpace(Program) ? FullName : $"{FullName} • {Program}";
    }

    public partial class ExamAccessModal : Window
    {
        private readonly Guid _examId;
        private readonly string _examTitle;
        private readonly string _examCode;
        private readonly ExamAccessService _accessService = new();

        private List<StudentSuggestionDto> _allActiveStudents = new();
        private bool _isSelectingSuggestion = false;

        public class AuthorizationDisplayItem
        {
            public Guid AuthorizationId { get; set; }
            public string StudentId { get; set; } = string.Empty;
            public string StudentName { get; set; } = string.Empty;
            public string StudentDetails { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime RequestedAt { get; set; }

            public bool CanApprove => Status == "PendingApproval" || Status == "Denied";
            public bool CanDeny => Status == "PendingApproval" || Status == "PreApproved" || Status == "Approved";

            public Brush StatusBgBrush => Status switch
            {
                "PreApproved" => new SolidColorBrush(Color.FromRgb(0xCF, 0xFA, 0xFE)),
                "Approved" => new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)),
                "PendingApproval" => new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)),
                "Denied" => new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)),
                _ => new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9))
            };

            public Brush StatusFgBrush => Status switch
            {
                "PreApproved" => new SolidColorBrush(Color.FromRgb(0x0E, 0x74, 0x90)),
                "Approved" => new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D)),
                "PendingApproval" => new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09)),
                "Denied" => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                _ => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            };
        }

        private ObservableCollection<AuthorizationDisplayItem> _authorizations = new();

        public ExamAccessModal(Guid examId, string examTitle, string examCode)
        {
            InitializeComponent();
            _examId = examId;
            _examTitle = examTitle;
            _examCode = examCode;

            HeaderTitleText.Text = $"Access Control • {examCode}";
            HeaderSubtitleText.Text = $"Manage whitelist and real-time approval queue for '{examTitle}'";

            AuthorizationsItemsControl.ItemsSource = _authorizations;

            Loaded += async (s, e) =>
            {
                await Task.WhenAll(LoadDataAsync(), LoadStudentsCacheAsync());
            };

            LocationChanged += (s, e) => { if (SuggestionsPopup != null) SuggestionsPopup.IsOpen = false; };
            Deactivated += (s, e) => { if (SuggestionsPopup != null) SuggestionsPopup.IsOpen = false; };
        }

        private async Task LoadStudentsCacheAsync()
        {
            try
            {
                using var context = new AppDbContext();
                _allActiveStudents = await context.Students
                    .AsNoTracking()
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .Select(s => new StudentSuggestionDto
                    {
                        StudentId = s.StudentId,
                        FullName = (s.FirstName + " " + s.LastName).Trim(),
                        Program = s.Program ?? (s.YearLevel ?? string.Empty)
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cache active students: {ex.Message}");
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var list = await _accessService.GetAuthorizationsForExamAsync(_examId);
                _authorizations.Clear();

                foreach (var auth in list)
                {
                    var studentName = auth.Student != null
                        ? $"{auth.Student.FirstName} {auth.Student.LastName}".Trim()
                        : "Unknown Student";

                    var details = auth.Student != null
                        ? $"Program: {auth.Student.Program ?? "N/A"} • Year: {auth.Student.YearLevel ?? "N/A"} • Requested: {auth.RequestedAt.ToLocalTime():g}"
                        : $"Requested: {auth.RequestedAt.ToLocalTime():g}";

                    _authorizations.Add(new AuthorizationDisplayItem
                    {
                        AuthorizationId = auth.AuthorizationId,
                        StudentId = auth.StudentId,
                        StudentName = studentName,
                        StudentDetails = details,
                        Status = auth.Status,
                        RequestedAt = auth.RequestedAt
                    });
                }

                EmptyListText.Visibility = _authorizations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                int approvedCount = _authorizations.Count(a => a.Status == "PreApproved" || a.Status == "Approved");
                int pendingCount = _authorizations.Count(a => a.Status == "PendingApproval");
                FooterStatsText.Text = $"{approvedCount} authorized / whitelisted • {pendingCount} pending approval";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading access list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StudentIdInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingSuggestion) return;

            var query = StudentIdInputBox.Text?.Trim() ?? string.Empty;
            if (query.Length < 1 || _allActiveStudents == null || _allActiveStudents.Count == 0)
            {
                SuggestionsPopup.IsOpen = false;
                return;
            }

            var existingIds = new HashSet<string>(
                _authorizations
                    .Where(a => a.Status == "PreApproved" || a.Status == "Approved" || a.Status == "PendingApproval")
                    .Select(a => a.StudentId),
                StringComparer.OrdinalIgnoreCase
            );

            var filtered = _allActiveStudents
                .Where(s => !existingIds.Contains(s.StudentId) &&
                            (s.StudentId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             s.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Take(8)
                .ToList();

            if (filtered.Count > 0)
            {
                SuggestionsListBox.ItemsSource = filtered;
                SuggestionsPopup.IsOpen = true;
            }
            else
            {
                SuggestionsPopup.IsOpen = false;
            }
        }

        private void ApplySuggestion(StudentSuggestionDto suggestion)
        {
            if (suggestion == null) return;
            _isSelectingSuggestion = true;
            StudentIdInputBox.Text = suggestion.StudentId;
            StudentIdInputBox.CaretIndex = StudentIdInputBox.Text.Length;
            _isSelectingSuggestion = false;

            SuggestionsPopup.IsOpen = false;
            SuggestionsListBox.SelectedItem = null;
            StudentIdInputBox.Focus();
        }

        private void SuggestionsListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not ListBoxItem)
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is ListBoxItem item && item.DataContext is StudentSuggestionDto suggestion)
            {
                ApplySuggestion(suggestion);
                e.Handled = true;
            }
        }

        private void SuggestionsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (SuggestionsListBox.SelectedItem is StudentSuggestionDto item)
                {
                    ApplySuggestion(item);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                SuggestionsPopup.IsOpen = false;
                StudentIdInputBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && SuggestionsListBox.SelectedIndex == 0)
            {
                SuggestionsPopup.IsOpen = false;
                StudentIdInputBox.Focus();
                e.Handled = true;
            }
        }

        private async void AddWhitelistBtn_Click(object sender, RoutedEventArgs e)
        {
            SuggestionsPopup.IsOpen = false;

            var studentId = StudentIdInputBox.Text.Trim();
            if (string.IsNullOrEmpty(studentId))
            {
                MessageBox.Show("Please enter a valid Student ID.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var instructorId = SessionManager.CurrentInstructor?.InstructorId ?? "INST-001";

            try
            {
                await _accessService.PreApproveStudentAsync(_examId, studentId, instructorId);
                StudentIdInputBox.Clear();
                await LoadDataAsync();
                ToastNotification.Show($"Student '{studentId}' added to whitelist!", Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to pre-approve student: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StudentIdInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && SuggestionsPopup.IsOpen && SuggestionsListBox.Items.Count > 0)
            {
                SuggestionsListBox.Focus();
                SuggestionsListBox.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SuggestionsPopup.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (SuggestionsPopup.IsOpen && SuggestionsListBox.SelectedItem is StudentSuggestionDto selected)
                {
                    ApplySuggestion(selected);
                    e.Handled = true;
                }
                else
                {
                    SuggestionsPopup.IsOpen = false;
                    AddWhitelistBtn_Click(sender, e);
                }
            }
        }

        private async void ApproveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid authId)
            {
                var instructorId = SessionManager.CurrentInstructor?.InstructorId ?? "INST-001";
                await _accessService.ApproveRequestAsync(authId, instructorId);
                await LoadDataAsync();
                ToastNotification.Show("Access request approved!", Application.Current.MainWindow);
            }
        }

        private async void DenyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid authId)
            {
                var instructorId = SessionManager.CurrentInstructor?.InstructorId ?? "INST-001";
                await _accessService.DenyRequestAsync(authId, instructorId);
                await LoadDataAsync();
                ToastNotification.Show("Access request denied.", Application.Current.MainWindow);
            }
        }

        private async void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid authId)
            {
                await _accessService.RemoveAuthorizationAsync(authId);
                await LoadDataAsync();
                ToastNotification.Show("Authorization removed.", Application.Current.MainWindow);
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            SuggestionsPopup.IsOpen = false;
            Close();
        }
    }
}

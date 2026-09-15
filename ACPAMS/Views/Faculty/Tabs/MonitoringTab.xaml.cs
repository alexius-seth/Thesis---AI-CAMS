using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ACPAMS.Views.Faculty.Tabs
{
    public partial class MonitoringTab : UserControl
    {
        private readonly AppDbContext _context;
        private readonly DispatcherTimer _refreshTimer;
        private Guid? _currentExamId;
        private Guid? _selectedAttemptId;

        // UI-bound collections
        public List<MonitoringService.ActiveStudentDto> ActiveStudents { get; set; } = new();
        public List<AuditLog> RecentFlags { get; set; } = new();
        public List<AuditLog> SelectedStudentEvents { get; set; } = new();
        public MonitoringService.MonitoringStatsDto Stats { get; set; } = new();

        public MonitoringTab() : this(null)
        {
        }

        public MonitoringTab(Guid? examId)
        {
            InitializeComponent();
            _context = new AppDbContext();
            _currentExamId = examId;

            DataContext = this;

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();
            _refreshTimer.Start();

            Loaded += async (s, e) => await RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                using var context = new AppDbContext();
                var monitoringService = new MonitoringService(Dispatcher);

                ActiveStudents = await monitoringService.GetActiveStudentsAsync(_currentExamId);
                RecentFlags = await monitoringService.GetRecentFlagsAsync(5);
                Stats = await monitoringService.GetStatisticsAsync(_currentExamId);

                Dispatcher.Invoke(() => UpdateUI());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            // Update KPIs
            var activeKpi = FindName("ActiveStationsKpi") as TextBlock;
            if (activeKpi != null) activeKpi.Text = Stats.ActiveStudents.ToString();

            var totalFlagsKpi = FindName("TotalFlagsKpi") as TextBlock;
            if (totalFlagsKpi != null) totalFlagsKpi.Text = Stats.TotalFlags.ToString();

            var highRiskKpi = FindName("HighRiskKpi") as TextBlock;
            if (highRiskKpi != null) highRiskKpi.Text = Stats.HighRiskFlags.ToString();

            // Update Active Students List
            var trackerItemsControl = FindName("TrackerItemsControl") as ItemsControl;
            if (trackerItemsControl != null)
            {
                // Preserve selection state
                foreach (var student in ActiveStudents)
                {
                    student.IsSelected = student.AttemptId == _selectedAttemptId;
                }
                trackerItemsControl.ItemsSource = ActiveStudents;
            }

            // Update Recent Flags Feed
            var feedItemsControl = FindName("FeedItemsControl") as ItemsControl;
            if (feedItemsControl != null) feedItemsControl.ItemsSource = RecentFlags.Take(15);

            // Update badges
            var activeBadge = FindName("ActiveBadge") as Border;
            if (activeBadge != null)
            {
                var badgeText = activeBadge.Child as TextBlock;
                if (badgeText != null) badgeText.Text = $"{Stats.ActiveStudents} Active";
            }

            var flagBadge = FindName("FlagBadge") as Border;
            if (flagBadge != null)
            {
                var badgeText = flagBadge.Child as TextBlock;
                if (badgeText != null) badgeText.Text = $"{Stats.TotalFlags} Flags";
            }

            // Update investigation pane if we have a selected student
            if (_selectedAttemptId.HasValue)
            {
                var selected = ActiveStudents.FirstOrDefault(s => s.AttemptId == _selectedAttemptId.Value);
                if (selected != null)
                {
                    UpdateInvestigationPane(selected);
                }
                else
                {
                    // Student no longer active, clear selection
                    _selectedAttemptId = null;
                    ClearInvestigationPane();
                }
            }
            else if (ActiveStudents.Any())
            {
                // Auto-select the first high-risk student
                var highRisk = ActiveStudents.FirstOrDefault(s => s.RiskLevel == "high");
                if (highRisk != null)
                {
                    _selectedAttemptId = highRisk.AttemptId;
                    highRisk.IsSelected = true;
                    UpdateInvestigationPane(highRisk);
                }
                else
                {
                    // If no high risk, select the first student
                    var first = ActiveStudents.FirstOrDefault();
                    if (first != null)
                    {
                        _selectedAttemptId = first.AttemptId;
                        first.IsSelected = true;
                        UpdateInvestigationPane(first);
                    }
                }
            }
            else
            {
                ClearInvestigationPane();
            }
        }

        private void ClearInvestigationPane()
        {
            var initials = FindName("SelectedStudentInitials") as TextBlock;
            if (initials != null) initials.Text = "?";

            var name = FindName("SelectedStudentName") as TextBlock;
            if (name != null) name.Text = "No student selected";

            var info = FindName("SelectedStudentInfo") as TextBlock;
            if (info != null) info.Text = "";

            var riskBadge = FindName("SelectedRiskBadge") as Border;
            if (riskBadge != null) riskBadge.Visibility = Visibility.Collapsed;

            var eventHistory = FindName("EventHistoryItemsControl") as ItemsControl;
            if (eventHistory != null) eventHistory.ItemsSource = null;

            var snapshot = FindName("SnapshotLabel") as TextBlock;
            if (snapshot != null) snapshot.Text = "Awaiting student activity...";
        }

        private async void StudentItem_Clicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Guid attemptId)
            {
                // Deselect all
                foreach (var student in ActiveStudents)
                {
                    student.IsSelected = false;
                }

                // Select the clicked student
                var selected = ActiveStudents.FirstOrDefault(s => s.AttemptId == attemptId);
                if (selected != null)
                {
                    _selectedAttemptId = attemptId;
                    selected.IsSelected = true;
                    UpdateInvestigationPane(selected);

                    // Refresh the tracker to show selection
                    var trackerItemsControl = FindName("TrackerItemsControl") as ItemsControl;
                    if (trackerItemsControl != null)
                    {
                        trackerItemsControl.ItemsSource = null;
                        trackerItemsControl.ItemsSource = ActiveStudents;
                    }
                }
            }
        }

        private async void UpdateInvestigationPane(MonitoringService.ActiveStudentDto student)
        {
            var initials = FindName("SelectedStudentInitials") as TextBlock;
            if (initials != null) initials.Text = student.StudentInitials;

            var name = FindName("SelectedStudentName") as TextBlock;
            if (name != null) name.Text = student.StudentName;

            var info = FindName("SelectedStudentInfo") as TextBlock;
            if (info != null)
            {
                info.Text = $"{student.DeviceName} · {student.FlagCount} flags";
                info.ToolTip = student.DeviceName;
            }

            var riskBadge = FindName("SelectedRiskBadge") as Border;
            var riskText = FindName("SelectedRiskText") as TextBlock;
            if (riskBadge != null && riskText != null)
            {
                riskBadge.Visibility = Visibility.Visible;
                riskText.Text = $"Risk: {student.RiskLevel.ToUpper()}";

                switch (student.RiskLevel)
                {
                    case "high":
                        riskBadge.Background = (SolidColorBrush)FindResource("Status.DangerBg");
                        riskBadge.BorderBrush = (SolidColorBrush)FindResource("Status.Danger");
                        riskText.Foreground = (SolidColorBrush)FindResource("Status.Danger");
                        break;
                    case "medium":
                        riskBadge.Background = (SolidColorBrush)FindResource("Status.WarningBg");
                        riskBadge.BorderBrush = (SolidColorBrush)FindResource("Status.Warning");
                        riskText.Foreground = (SolidColorBrush)FindResource("Status.Warning");
                        break;
                    default:
                        riskBadge.Background = (SolidColorBrush)FindResource("Status.SuccessBg");
                        riskBadge.BorderBrush = (SolidColorBrush)FindResource("Status.Success");
                        riskText.Foreground = (SolidColorBrush)FindResource("Status.Success");
                        break;
                }
            }

            // Load event history for this student
            await LoadStudentEvents(student.AttemptId);

            var snapshot = FindName("SnapshotLabel") as TextBlock;
            if (snapshot != null)
            {
                var recentFlag = student.RecentFlags.FirstOrDefault();
                snapshot.Text = recentFlag != null
                    ? $"Last event: {recentFlag.Action} at {recentFlag.CreatedAt:HH:mm}"
                    : "Awaiting student activity...";
            }
        }

        private async Task LoadStudentEvents(Guid attemptId)
        {
            try
            {
                using var context = new AppDbContext();
                var events = await context.AuditLogs
                    .Where(l => l.EntityId == attemptId.ToString()
                                && l.EntityType == "StudentExamAttempt"
                                && !l.Action.Contains("HeadTurned")
                                && !l.Action.Contains("HeadTilted"))
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                var eventHistory = FindName("EventHistoryItemsControl") as ItemsControl;
                if (eventHistory != null)
                {
                    eventHistory.ItemsSource = events;
                }

                SelectedStudentEvents = events;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading events: {ex.Message}");
            }
        }

        private async void IssueWarningFromInvestigation_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAttemptId.HasValue)
            {
                await IssueWarningAsync(_selectedAttemptId.Value);
            }
            else
            {
                MessageBox.Show("Please select a student first.", "No Student Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task IssueWarningAsync(Guid attemptId)
        {
            var result = MessageBox.Show("Issue a warning to this student?", "Issue Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var context = new AppDbContext();
                    var monitoringService = new MonitoringService(Dispatcher);

                    await monitoringService.IssueWarningAsync(
                        attemptId,
                        "behavioral",
                        "Instructor issued a warning for suspicious behavior",
                        SessionManager.CurrentUser?.UserId
                    );

                    var student = ActiveStudents.FirstOrDefault(s => s.AttemptId == attemptId);
                    ToastNotification.Show(
                        $"⚠️ Warning issued to {student?.StudentName ?? "student"}",
                        Application.Current.MainWindow,
                        3,
                        true
                    );

                    await RefreshDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to issue warning: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ─── Dismiss Flag - Delete ALL flags for the selected student ───
        private async void DismissFlagFromInvestigation_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedAttemptId.HasValue)
            {
                MessageBox.Show("Please select a student first.", "No Student Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "This will delete ALL flags for this student. Are you sure?",
                "Dismiss All Flags",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                await DismissAllFlagsForStudent(_selectedAttemptId.Value);
            }
        }

        private async Task DismissAllFlagsForStudent(Guid attemptId)
        {
            try
            {
                using var context = new AppDbContext();

                // Get all flags for this student (excluding head movement events)
                var flags = await context.AuditLogs
                    .Where(l => l.EntityId == attemptId.ToString()
                                && l.EntityType == "StudentExamAttempt"
                                && !l.Action.Contains("HeadTurned")
                                && !l.Action.Contains("HeadTilted"))
                    .ToListAsync();

                if (flags.Count == 0)
                {
                    MessageBox.Show("No flags to dismiss for this student.", "No Flags", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Delete all flags
                context.AuditLogs.RemoveRange(flags);
                await context.SaveChangesAsync();

                // Also delete corresponding ExamAttemptLogs
                var attemptLogs = await context.ExamAttemptLogs
                    .Where(l => l.AttemptId == attemptId)
                    .ToListAsync();

                if (attemptLogs.Any())
                {
                    context.ExamAttemptLogs.RemoveRange(attemptLogs);
                    await context.SaveChangesAsync();
                }

                ToastNotification.Show(
                    $"✅ Dismissed {flags.Count} flags for this student",
                    Application.Current.MainWindow,
                    3,
                    false
                );

                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to dismiss flags: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Dismiss Single Flag (from the feed) ───
        private async void DismissSingleFlag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid logId)
            {
                var result = MessageBox.Show(
                    "Dismiss this flag?",
                    "Dismiss Flag",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    await DismissSingleFlagAsync(logId);
                }
            }
        }

        private async Task DismissSingleFlagAsync(Guid logId)
        {
            try
            {
                using var context = new AppDbContext();
                var log = await context.AuditLogs.FindAsync(logId);
                if (log != null)
                {
                    context.AuditLogs.Remove(log);
                    await context.SaveChangesAsync();

                    // Also delete corresponding ExamAttemptLog
                    var attemptLog = await context.ExamAttemptLogs
                        .FirstOrDefaultAsync(l => l.Action == log.Action
                                                   && l.Details == log.Details
                                                   && l.ActionTime == log.CreatedAt);
                    if (attemptLog != null)
                    {
                        context.ExamAttemptLogs.Remove(attemptLog);
                        await context.SaveChangesAsync();
                    }

                    ToastNotification.Show("Flag dismissed", Application.Current.MainWindow, 2);
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to dismiss flag: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewFullReport_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAttemptId.HasValue)
            {
                try
                {
                    // Navigate to the student report page
                    var reportPage = new StudentReportPage(_selectedAttemptId.Value);
                    MainWindow.Navigate(reportPage);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a student first.", "No Student Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDataAsync();
            ToastNotification.Show("Data refreshed", Application.Current.MainWindow, 2);
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            _context?.Dispose();
        }

        private void BackToHome_Click(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            MainWindow.NavigateBackStatic();
        }
    }
}
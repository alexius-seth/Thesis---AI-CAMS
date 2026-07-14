using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty.Tabs
{
    public partial class ReportsTab : UserControl, IRefreshableView
    {
        private readonly Guid? _examId;
        private readonly DispatcherTimer _autoRefreshTimer;

        public ReportsTab() : this(null) { }

        public ReportsTab(Guid? examId)
        {
            InitializeComponent();
            _examId = examId;

            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoRefreshTimer.Tick += async (s, e) => await LoadReportsAsync();

            Loaded += ReportsTab_Loaded;
            Unloaded += ReportsTab_Unloaded;
        }

        private async void ReportsTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown += Window_PreviewKeyDown;

            _autoRefreshTimer.Start();

            await LoadReportsAsync();
        }

        private void ReportsTab_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown -= Window_PreviewKeyDown;

            _autoRefreshTimer.Stop();
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await LoadReportsAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadReportsAsync()
        {
            var service = new ReportsService();
            ReportsSummaryDto summary;

            if (_examId.HasValue)
            {
                // Scoped to one exam — only students who took THIS exam show up.
                summary = await service.GetSummaryForExamAsync(_examId.Value);
            }
            else
            {
                // No exam specified (generic "View Student Reports" quick-nav) —
                // browse across every exam this instructor owns.
                var instructor = SessionManager.CurrentInstructor;
                if (instructor == null)
                {
                    EmptyStateBanner.Visibility = Visibility.Visible;
                    return;
                }

                summary = await service.GetSummaryAsync(instructor.InstructorId);
            }

            // ── KPIs ──
            if (summary.HasClassAverage)
            {
                ClassAverageValue.Text = $"{summary.ClassAveragePercent:0.#}%";
                ClassAverageSub.Text = $"Across {summary.GradedCount} graded submission{(summary.GradedCount == 1 ? "" : "s")}";
            }

            if (summary.HasCompletionRate)
            {
                CompletionRateValue.Text = $"{summary.CompletionRatePercent:0}%";
                CompletionRateSub.Text = $"{summary.TotalAttempts} total attempt{(summary.TotalAttempts == 1 ? "" : "s")}";
            }

            if (summary.HasIntegrityIndex)
            {
                IntegrityIndexValue.Text = $"{summary.IntegrityIndexPercent:0}%";
                IntegrityIndexSub.Text = summary.IntegrityIndexPercent >= 80 ? "Low anomaly rate" : "Elevated anomaly rate";
            }

            // ── Infraction Breakdown legend + center total (real counts; wedge shapes stay illustrative) ──
            InfractionTotalLabel.Text = summary.TotalInfractions.ToString();
            GazeDeviationCount.Text = summary.GazeDeviationCount.ToString();
            WindowSwitchCount.Text = summary.WindowSwitchCount.ToString();
            ClipboardCount.Text = summary.ClipboardCount.ToString();

            // ── Detailed Performance Data table ──
            BuildReportRows(summary.Rows);
        }

        private void BuildReportRows(System.Collections.Generic.List<ReportRowDto> rows)
        {
            ReportRowsPanel.Children.Clear();

            if (rows.Count == 0)
            {
                EmptyStateBanner.Visibility = Visibility.Visible;
                return;
            }

            EmptyStateBanner.Visibility = Visibility.Collapsed;

            foreach (var r in rows)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

                var avatar = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = Brush("#E2E8F0"),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock
                    {
                        Text = r.Initials,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
                namePanel.Children.Add(avatar);
                namePanel.Children.Add(new TextBlock { Text = r.StudentName, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
                Grid.SetColumn(namePanel, 0);

                // AI Score / AI Conf — pending AI integration, honestly shown as "—"
                var aiScore = new TextBlock { Text = "—", FontSize = 13, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(aiScore, 1);

                var finalGrade = new TextBlock
                {
                    Text = r.FinalScoreText,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = r.IsPassing ? Brush("#15803D") : Brush("#B45309"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(finalGrade, 2);

                var aiConf = new TextBlock { Text = "—", FontSize = 13, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(aiConf, 3);

                var (riskFg, riskBg) = RiskColors(r.RiskLevel);
                var anomalyBadge = new Border
                {
                    Background = riskBg,
                    BorderBrush = riskFg,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = r.AnomalyCount == 0 ? "0 anomalies" : $"{r.AnomalyCount}  ·  {r.RiskLevel}",
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = riskFg
                    }
                };
                Grid.SetColumn(anomalyBadge, 4);

                var viewButton = new Button
                {
                    Content = "View Report",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = r.AttemptId
                };
                if (TryFindResource("Btn.Outlined") is Style outlinedStyle)
                    viewButton.Style = outlinedStyle;
                viewButton.Click += ViewReport_Click;
                Grid.SetColumn(viewButton, 5);

                grid.Children.Add(namePanel);
                grid.Children.Add(aiScore);
                grid.Children.Add(finalGrade);
                grid.Children.Add(aiConf);
                grid.Children.Add(anomalyBadge);
                grid.Children.Add(viewButton);

                var row = new Border
                {
                    BorderBrush = Brush("#E2E8F0"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(16, 12, 16, 12),
                    Child = grid
                };

                ReportRowsPanel.Children.Add(row);
            }
        }

        // Called by MainWindow.ShowPage on every navigation to this page —
        // matters especially for the cached, unscoped ReportsTab instance
        // (the generic "View Student Reports" quick-nav).
        public async System.Threading.Tasks.Task RefreshAsync()
        {
            await LoadReportsAsync();
        }

        private void ViewReport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid attemptId)
            {
                MainWindow.NavigateToStudentReport(attemptId);
            }
        }

        private static (Brush fg, Brush bg) RiskColors(string risk) => risk switch
        {
            "High" => (Brush("#C62828"), Brush("#FEF2F2")),
            "Medium" => (Brush("#B45309"), Brush("#FEF3C7")),
            _ => (Brush("#15803D"), Brush("#DCFCE7"))
        };

        private static Brush Brush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Returns to whichever page opened Reports, via MainWindow's navigation
            // history — same pattern as Monitoring/Grading's Back buttons.
            MainWindow.NavigateBackStatic();
        }
    }
}
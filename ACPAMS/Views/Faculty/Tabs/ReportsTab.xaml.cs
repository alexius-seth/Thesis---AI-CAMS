using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ACPAMS.Data;
using ACPAMS.Services;
using ACPAMS.Views.Faculty;

namespace ACPAMS.Views.Faculty.Tabs
{
    public partial class ReportsTab : UserControl, IRefreshableView
    {
        private readonly Guid? _examId;
        // Set only when this page is opened generically (no _examId) and the
        // instructor picks something from ExamSelectorCombo. Separate from
        // _examId because that one represents "opened already scoped to a
        // specific exam via its own View Report button" — a different,
        // earlier decision point than picking from this page's dropdown.
        private Guid? _selectedGenericExamId;

        // Whichever exam is actually in effect right now, regardless of
        // which of the two selection paths produced it.
        private Guid? EffectiveExamId => _examId ?? _selectedGenericExamId;
        private readonly DispatcherTimer _autoRefreshTimer;

        private ReportsSummaryDto? _lastSummary;
        private List<SectionAverageDto>? _lastSectionAverages;
        private string _currentExamTitle = "Exam Report";

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
                // Opened via a specific exam's "View Report" button — the
                // instructor already made their choice by clicking it.
                // Statistics show immediately, no selector needed.
                ExamSelectorContainer.Visibility = Visibility.Collapsed;
                StatisticsSections.Visibility = Visibility.Visible;

                summary = await service.GetSummaryForExamAsync(_examId.Value);

                if (_currentExamTitle == "Exam Report")
                {
                    using var db = new AppDbContext();
                    var exam = await db.Exams.FindAsync(_examId.Value);
                    if (exam != null)
                        _currentExamTitle = exam.ExamName ?? exam.Title ?? "Exam Report";
                }
            }
            else
            {
                // Generic "View Student Reports" quick-nav — require an
                // explicit exam choice before showing anything, instead of
                // silently defaulting to whichever exam happens to be most
                // recent. The dropdown itself only needs populating once.
                var instructor = SessionManager.CurrentInstructor;
                if (instructor == null)
                {
                    EmptyStateBanner.Visibility = Visibility.Visible;
                    return;
                }

                if (ExamSelectorCombo.ItemsSource == null)
                {
                    var exams = await service.GetInstructorExamsAsync(instructor.InstructorId);
                    ExamSelectorCombo.ItemsSource = exams;
                }

                ExamSelectorContainer.Visibility = Visibility.Visible;

                if (!_selectedGenericExamId.HasValue)
                {
                    // Nothing chosen yet — show the selector, hide everything else.
                    StatisticsSections.Visibility = Visibility.Collapsed;
                    return;
                }

                StatisticsSections.Visibility = Visibility.Visible;
                summary = await service.GetSummaryForExamAsync(_selectedGenericExamId.Value);
            }

            _lastSummary = summary;

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

            UpdateInfractionWedges(summary.GazeDeviationCount, summary.WindowSwitchCount, summary.ClipboardCount);

            // ── Section Performance (real, replaces the old placeholder radar) ──
            await LoadSectionPerformanceAsync(service);

            // ── Detailed Performance Data table ──
            BuildReportRows(summary.Rows);
        }

        // Only meaningful for ONE specific exam — sections belong to a single
        // exam's structure, so this has nothing sensible to show in the
        // generic "browse all exams" mode (_examId == null).
        private async System.Threading.Tasks.Task LoadSectionPerformanceAsync(ReportsService service)
        {
            if (!EffectiveExamId.HasValue)
            {
                SectionPerformanceList.ItemsSource = null;
                SectionPerformanceEmptyHint.Text = "Open a specific exam's report to see section performance.";
                SectionPerformanceEmptyHint.Visibility = Visibility.Visible;
                return;
            }

            var sectionAverages = await service.GetSectionAveragesForExamAsync(EffectiveExamId.Value);
            _lastSectionAverages = sectionAverages;
            var withData = sectionAverages.Where(s => s.HasData).ToList();

            if (withData.Count == 0)
            {
                SectionPerformanceList.ItemsSource = null;
                SectionPerformanceEmptyHint.Text = "No fully-graded sections yet.";
                SectionPerformanceEmptyHint.Visibility = Visibility.Visible;
                return;
            }

            SectionPerformanceEmptyHint.Visibility = Visibility.Collapsed;

            // BarWidth is in pixels against the ItemsControl's ~200px available
            // track width (Grid.Column="*" between the 70px label and 36px
            // percent columns) — scaled directly off AveragePercent (0-100).
            const double maxBarWidth = 200;

            SectionPerformanceList.ItemsSource = withData.Select(s => new SectionPerformanceRowVM
            {
                SectionName = s.SectionName,
                PercentText = $"{s.AveragePercent:0.#}%",
                BarWidth = Math.Max(4, (double)s.AveragePercent / 100.0 * maxBarWidth)
            }).ToList();
        }

        // ── Compute real donut wedge proportions from actual infraction counts ──
        // A WPF Ellipse stroke can fake a pie wedge via StrokeDashArray: set a
        // "dash" (visible) length and "gap" (invisible) length in units of
        // StrokeThickness, then rotate the whole ellipse so the dash starts
        // at the right position. For a circle of radius r with stroke
        // thickness t, the full circumference in dash-array units is
        // 2πr / t. Each segment's dash length is (its share of the total) ×
        // that circumference; everything else is gap. Rotation starts each
        // segment exactly where the previous one ended, beginning at 12
        // o'clock (-90°) and proceeding clockwise.
        private void UpdateInfractionWedges(int gazeCount, int windowCount, int clipboardCount)
        {
            const double radius = 60;
            const double strokeThickness = 40;
            double circumferenceUnits = 2 * Math.PI * radius / strokeThickness;

            int total = gazeCount + windowCount + clipboardCount;

            if (total == 0)
            {
                // Nothing to show — collapse all three so only the base
                // (empty) circle + center label remain visible.
                GazeSegment.StrokeDashArray = new DoubleCollection { 0, circumferenceUnits };
                WindowSegment.StrokeDashArray = new DoubleCollection { 0, circumferenceUnits };
                ClipboardSegment.StrokeDashArray = new DoubleCollection { 0, circumferenceUnits };
                return;
            }

            double gazeFraction = (double)gazeCount / total;
            double windowFraction = (double)windowCount / total;
            double clipboardFraction = (double)clipboardCount / total;

            SetWedge(GazeSegment, GazeSegmentRotate, gazeFraction, cumulativeFractionBefore: 0, circumferenceUnits);
            SetWedge(WindowSegment, WindowSegmentRotate, windowFraction, cumulativeFractionBefore: gazeFraction, circumferenceUnits);
            SetWedge(ClipboardSegment, ClipboardSegmentRotate, clipboardFraction, cumulativeFractionBefore: gazeFraction + windowFraction, circumferenceUnits);
        }

        private static void SetWedge(Ellipse segment, RotateTransform rotate, double fraction,
            double cumulativeFractionBefore, double circumferenceUnits)
        {
            double dashLength = fraction * circumferenceUnits;
            double gapLength = circumferenceUnits - dashLength;

            segment.StrokeDashArray = new DoubleCollection { dashLength, gapLength };
            rotate.Angle = -90 + (cumulativeFractionBefore * 360);
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
                var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Cursor = System.Windows.Input.Cursors.Hand };
                namePanel.Children.Add(avatar);
                var nameLink = new TextBlock
                {
                    Text = r.StudentName,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("#1976D2"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextDecorations = TextDecorations.Underline
                };
                string clickedStudentId = r.StudentId; // captured per-row for the closure below
                namePanel.MouseLeftButtonUp += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(clickedStudentId))
                        MainWindow.Navigate(new StudentInformationTab(clickedStudentId));
                };
                namePanel.Children.Add(nameLink);
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

        private async void ExamSelectorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExamSelectorCombo.SelectedItem is ExamOptionDto selected)
            {
                _selectedGenericExamId = selected.ExamId;
                _currentExamTitle = selected.Title;
                await LoadReportsAsync();
            }
        }

        private void ExportClassPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSummary == null || (_lastSummary.TotalAttempts == 0 && _lastSummary.Rows.Count == 0))
            {
                MessageBox.Show("No class report data available to export.", "Export PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var instructor = SessionManager.CurrentInstructor;
            string instructorName = instructor != null ? $"{instructor.FirstName} {instructor.LastName}" : "Faculty Instructor";

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"{(string.IsNullOrWhiteSpace(_currentExamTitle) ? "Exam" : _currentExamTitle.Replace(" ", "_"))}_Class_Report.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    PdfExportService.ExportClassReport(_lastSummary, _lastSectionAverages, _currentExamTitle, instructorName, saveDialog.FileName);
                    MessageBox.Show("Class performance report exported successfully!", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export PDF: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Returns to whichever page opened Reports, via MainWindow's navigation
            // history — same pattern as Monitoring/Grading's Back buttons.
            MainWindow.NavigateBackStatic();
        }
    }

    // Backs the Section Performance ItemsControl — one row per exam section,
    // with a pre-computed pixel bar width so the XAML template stays a plain binding.
    public class SectionPerformanceRowVM
    {
        public string SectionName { get; set; } = string.Empty;
        public string PercentText { get; set; } = string.Empty;
        public double BarWidth { get; set; }
    }
}

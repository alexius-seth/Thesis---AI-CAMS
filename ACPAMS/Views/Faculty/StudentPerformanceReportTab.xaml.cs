using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty
{
    public partial class StudentPerformanceReportTab : UserControl
    {
        private readonly string _studentId;
        private readonly NavigationOrigin _origin;
        private readonly Guid? _previousExamId;

        public StudentPerformanceReportTab(string studentId, NavigationOrigin origin = NavigationOrigin.Search, Guid? previousExamId = null)
        {
            InitializeComponent();
            _studentId = studentId;
            _origin = origin;
            _previousExamId = previousExamId;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            var infoService = new StudentInformationService();
            var perfService = new StudentPerformanceService();

            var profile = await infoService.GetStudentProfileSummaryAsync(_studentId);
            StudentNameText.Text = profile.StudentName;
            ProgramText.Text = profile.Program;

            var metrics = await perfService.GetMetricsAsync(_studentId);
            ConceptMasteryValue.Text = metrics.HasConceptMastery ? $"{metrics.ConceptMasteryPercent:0.#}%" : "—";
            AiConfidenceValue.Text = metrics.HasAiConfidence ? $"{metrics.AiConfidencePercent:0}%" : "—";

            if (metrics.HasRiskScore)
            {
                RiskScoreValue.Text = metrics.RiskScore.ToString();
                var (fg, bg) = metrics.RiskLevel switch
                {
                    "High" => (Color.FromRgb(0xC6, 0x28, 0x28), Color.FromRgb(0xFE, 0xF2, 0xF2)),
                    "Medium" => (Color.FromRgb(0xB4, 0x53, 0x09), Color.FromRgb(0xFE, 0xF3, 0xC7)),
                    _ => (Color.FromRgb(0x15, 0x80, 0x3D), Color.FromRgb(0xDC, 0xFC, 0xE7))
                };
                RiskLevelBadge.BorderBrush = new SolidColorBrush(fg);
                RiskLevelBadge.Background = new SolidColorBrush(bg);
                RiskLevelText.Foreground = new SolidColorBrush(fg);
                RiskLevelText.Text = $"{metrics.RiskLevel} Risk";
            }
            else
            {
                RiskScoreValue.Text = "—";
                RiskLevelBadge.Visibility = Visibility.Collapsed;
            }

            await LoadQuestionTypePerformanceAsync(perfService);
            await LoadBehavioralInsightsAsync(perfService);
            await LoadEvaluationLedgerAsync(perfService);
        }

        private async System.Threading.Tasks.Task LoadQuestionTypePerformanceAsync(StudentPerformanceService service)
        {
            var types = await service.GetQuestionTypePerformanceAsync(_studentId);

            if (types.Count == 0)
            {
                QuestionTypeEmptyHint.Visibility = Visibility.Visible;
                QuestionTypeList.ItemsSource = null;
                return;
            }
            QuestionTypeEmptyHint.Visibility = Visibility.Collapsed;

            const double maxBarWidth = 160;
            QuestionTypeList.ItemsSource = types.ConvertAll(t => new
            {
                t.QuestionType,
                PercentText = $"{t.AveragePercent:0.#}%",
                BarWidth = Math.Max(4, (double)t.AveragePercent / 100.0 * maxBarWidth)
            });
        }

        private async System.Threading.Tasks.Task LoadBehavioralInsightsAsync(StudentPerformanceService service)
        {
            var insights = await service.GetBehavioralInsightsAsync(_studentId);

            if (insights.SessionCount == 0)
            {
                GazeInsightText.Text = "No completed sessions yet.";
                WindowInsightText.Text = "No completed sessions yet.";
                ClipboardInsightText.Text = "No completed sessions yet.";
                return;
            }

            GazeInsightText.Text = insights.GazeDeviationCount == 0
                ? $"No gaze deviation flags across {insights.SessionCount} session{(insights.SessionCount == 1 ? "" : "s")}."
                : $"{insights.GazeDeviationCount} gaze deviation event{(insights.GazeDeviationCount == 1 ? "" : "s")} across {insights.SessionCount} session{(insights.SessionCount == 1 ? "" : "s")}.";

            WindowInsightText.Text = insights.WindowSwitchCount == 0
                ? $"Zero window/tab-switch events across {insights.SessionCount} session{(insights.SessionCount == 1 ? "" : "s")}."
                : $"{insights.WindowSwitchCount} window/tab-switch event{(insights.WindowSwitchCount == 1 ? "" : "s")} across {insights.SessionCount} session{(insights.SessionCount == 1 ? "" : "s")}.";

            ClipboardInsightText.Text = insights.ClipboardCount == 0
                ? "No clipboard access or paste attempts recorded."
                : $"{insights.ClipboardCount} clipboard access/paste attempt{(insights.ClipboardCount == 1 ? "" : "s")} recorded.";
        }

        private async System.Threading.Tasks.Task LoadEvaluationLedgerAsync(StudentPerformanceService service)
        {
            LedgerRowsPanel.Children.Clear();

            var rows = await service.GetEvaluationLedgerAsync(_studentId);

            if (rows.Count == 0)
            {
                LedgerEmptyHint.Visibility = Visibility.Visible;
                return;
            }
            LedgerEmptyHint.Visibility = Visibility.Collapsed;

            foreach (var row in rows)
            {
                var container = new Border
                {
                    BorderBrush = (Brush)FindResource("Border.Subtle"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(16, 12, 16, 12)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var nameText = new TextBlock
                {
                    Text = row.ExamName,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Text.Primary"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(nameText, 0);

                var scoreText = new TextBlock
                {
                    Text = row.ScoreText,
                    FontSize = 12,
                    Foreground = (Brush)FindResource("Text.Secondary"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(scoreText, 1);

                var tagsPanel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
                tagsPanel.Children.Add(new TextBlock
                {
                    Text = $"+ {row.StrengthsCount} Strength{(row.StrengthsCount == 1 ? "" : "s")}",
                    FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Status.Success")
                });
                tagsPanel.Children.Add(new TextBlock
                {
                    Text = $"- {row.WeaknessesCount} Weakness{(row.WeaknessesCount == 1 ? "" : "es")}",
                    FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Status.Danger"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(tagsPanel, 2);

                var summaryBorder = new Border
                {
                    Background = (Brush)FindResource("AI.Bg"),
                    BorderBrush = (Brush)FindResource("AI.Border"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6),
                    Child = new TextBlock
                    {
                        Text = row.SummarySnippet,
                        FontSize = 11,
                        Foreground = (Brush)FindResource("AI.Text"),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(summaryBorder, 3);

                grid.Children.Add(nameText);
                grid.Children.Add(scoreText);
                grid.Children.Add(tagsPanel);
                grid.Children.Add(summaryBorder);

                container.Child = grid;
                LedgerRowsPanel.Children.Add(container);
            }
        }

        private void GoToInformation_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(new StudentInformationTab(_studentId, _origin, _previousExamId));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_origin)
            {
                case NavigationOrigin.Reports:
                    MainWindow.NavigateToReportsStatic(_previousExamId);
                    break;
                case NavigationOrigin.Dashboard:
                    MainWindow.NavigateToDashboardStatic();
                    break;
                case NavigationOrigin.Search:
                default:
                    MainWindow.NavigateToFacultyHomeStatic();
                    break;
            }
        }
    }
}

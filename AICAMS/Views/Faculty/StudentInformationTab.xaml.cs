using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty
{
    public partial class StudentInformationTab : UserControl
    {
        private readonly string _studentId;
        private readonly NavigationOrigin _origin;
        private readonly Guid? _previousExamId;
        private StudentProfileSummaryDto? _summary;

        public StudentInformationTab(string studentId, NavigationOrigin origin = NavigationOrigin.Search, Guid? previousExamId = null)
        {
            InitializeComponent();
            _studentId = studentId;
            _origin = origin;
            _previousExamId = previousExamId;
            Loaded += async (s, e) => await LoadAsync();
            SizeChanged += (s, e) => DrawTrend(); // redraw if the window resizes
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            var service = new StudentInformationService();
            _summary = await service.GetStudentProfileSummaryAsync(_studentId);

            StudentNameText.Text = _summary.StudentName;
            ProgramText.Text = _summary.Program;
            StudentIdText.Text = $"ID: {_summary.StudentId}";
            SectionText.Text = $"Section: {_summary.Section}";

            TotalAssessmentsValue.Text = _summary.TotalAssessments.ToString();

            if (_summary.HasOverallAverage)
            {
                OverallAverageValue.Text = $"{_summary.OverallAveragePercent:0.#}%";
                ApplyStandingBadge(StandingBadge, StandingBadgeText, _summary.AcademicStanding!);
                ApplyStandingBadge(StandingBadge2, StandingBadge2Text, _summary.AcademicStanding!);
            }
            else
            {
                OverallAverageValue.Text = "—";
                StandingBadge.Visibility = Visibility.Collapsed;
                ApplyStandingBadge(StandingBadge2, StandingBadge2Text, "No Data");
            }

            TrendCountBadge.Text = $"{_summary.PerformanceTrend.Count} Assessment{(_summary.PerformanceTrend.Count == 1 ? "" : "s")}";
            DrawTrend();

            BuildAssessmentRows();
        }

        private static void ApplyStandingBadge(Border badge, TextBlock text, string standing)
        {
            text.Text = standing;
            var (fg, bg) = standing switch
            {
                "Excellent" => (Color.FromRgb(0x15, 0x80, 0x3D), Color.FromRgb(0xDC, 0xFC, 0xE7)),
                "Good" => (Color.FromRgb(0x19, 0x76, 0xD2), Color.FromRgb(0xEF, 0xF6, 0xFF)),
                "Fair" => (Color.FromRgb(0xB4, 0x53, 0x09), Color.FromRgb(0xFE, 0xF3, 0xC7)),
                "Needs Improvement" => (Color.FromRgb(0xC6, 0x28, 0x28), Color.FromRgb(0xFE, 0xF2, 0xF2)),
                _ => (Color.FromRgb(0x64, 0x74, 0x8B), Color.FromRgb(0xF8, 0xFA, 0xFC))
            };
            badge.BorderBrush = new SolidColorBrush(fg);
            badge.Background = new SolidColorBrush(bg);
            text.Foreground = new SolidColorBrush(fg);
        }

        // Simple polyline trend chart — deliberately not a full charting
        // library dependency, just points scaled to the canvas's actual size.
        private void DrawTrend()
        {
            if (_summary == null) return;

            TrendCanvas.Children.Clear();

            var points = _summary.PerformanceTrend;
            if (points.Count < 2)
            {
                TrendEmptyHint.Visibility = Visibility.Visible;
                return;
            }
            TrendEmptyHint.Visibility = Visibility.Collapsed;

            double width = TrendCanvas.ActualWidth;
            double height = TrendCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            const double padding = 8;
            double usableWidth = width - padding * 2;
            double usableHeight = height - padding * 2;

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)),
                StrokeThickness = 2
            };

            for (int i = 0; i < points.Count; i++)
            {
                double x = padding + (points.Count == 1 ? 0 : (double)i / (points.Count - 1) * usableWidth);
                double normalizedPercent = Math.Clamp((double)points[i].Percent, 0, 100) / 100.0;
                double y = padding + (1 - normalizedPercent) * usableHeight;

                polyline.Points.Add(new Point(x, y));

                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2))
                };
                Canvas.SetLeft(dot, x - 3);
                Canvas.SetTop(dot, y - 3);
                TrendCanvas.Children.Add(dot);
            }

            TrendCanvas.Children.Insert(0, polyline);
        }

        private void BuildAssessmentRows()
        {
            AssessmentRowsPanel.Children.Clear();

            if (_summary == null || _summary.AssessmentRecords.Count == 0)
            {
                AssessmentsEmptyHint.Visibility = Visibility.Visible;
                return;
            }
            AssessmentsEmptyHint.Visibility = Visibility.Collapsed;

            foreach (var record in _summary.AssessmentRecords)
            {
                var row = new Border
                {
                    BorderBrush = (Brush)FindResource("Border.Subtle"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(16, 12, 16, 12)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

                var nameText = new TextBlock
                {
                    Text = record.ExamName,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Text.Primary"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(nameText, 0);

                var dateText = new TextBlock
                {
                    Text = record.DateTaken.ToString("yyyy-MM-dd"),
                    FontSize = 12,
                    Foreground = (Brush)FindResource("Text.Secondary"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dateText, 1);

                var typeBadge = new Border
                {
                    Background = (Brush)FindResource("Bg.TableHeader"),
                    BorderBrush = (Brush)FindResource("Border.Medium"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = record.TypesSummary,
                        FontSize = 11,
                        Foreground = (Brush)FindResource("Text.Secondary")
                    }
                };
                Grid.SetColumn(typeBadge, 2);

                var gradeText = new TextBlock
                {
                    Text = record.FinalGradeText,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Text.Primary"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(gradeText, 3);

                var (badgeFg, badgeBg) = record.Passed
                    ? (Color.FromRgb(0x15, 0x80, 0x3D), Color.FromRgb(0xDC, 0xFC, 0xE7))
                    : (Color.FromRgb(0xC6, 0x28, 0x28), Color.FromRgb(0xFE, 0xF2, 0xF2));

                var statusBadge = new Border
                {
                    Background = new SolidColorBrush(badgeBg),
                    BorderBrush = new SolidColorBrush(badgeFg),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = record.Status,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(badgeFg)
                    }
                };
                Grid.SetColumn(statusBadge, 4);

                grid.Children.Add(nameText);
                grid.Children.Add(dateText);
                grid.Children.Add(typeBadge);
                grid.Children.Add(gradeText);
                grid.Children.Add(statusBadge);

                row.Child = grid;
                AssessmentRowsPanel.Children.Add(row);
            }
        }

        private void GoToPerformanceReport_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(new StudentPerformanceReportTab(_studentId, _origin, _previousExamId));
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

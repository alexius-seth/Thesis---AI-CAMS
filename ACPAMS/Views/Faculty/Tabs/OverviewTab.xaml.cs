using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty.Tabs
{
    public partial class OverviewTab : UserControl
    {
        public OverviewTab()
        {
            InitializeComponent();
        }

        public async Task LoadAsync(LiveExamDto liveExam)
        {
            var service = new DashboardService();

            var rows = await service.GetStudentRowsAsync(liveExam.ExamId);
            var dist = await service.AutoGradeAndGetDistributionAsync(liveExam.ExamId);

            ExamLabel.Text = $"{liveExam.CourseCode} — {liveExam.Title}".ToUpper();
            StudentsTakingValue.Text = rows.Count(r => r.Status == "in_progress").ToString();
            StudentsFinishedValue.Text = rows.Count(r => r.Status == "submitted" || r.Status == "graded").ToString();
            StudentsFinishedSub.Text = $"of {rows.Count} enrolled";

            BuildStudentRows(rows);
            BuildScoreDistribution(dist);
        }

        private void BuildStudentRows(List<StudentRowDto> rows)
        {
            StudentRowsPanel.Children.Clear();

            if (rows.Count == 0)
            {
                StudentRowsPanel.Children.Add(new TextBlock
                {
                    Text = "No students have started this exam yet.",
                    FontSize = 13,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(16, 12, 16, 12)
                });
                return;
            }

            foreach (var r in rows)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var avatar = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock
                    {
                        Text = r.Initials,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
                namePanel.Children.Add(avatar);
                namePanel.Children.Add(new TextBlock
                {
                    Text = r.StudentName,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetColumn(namePanel, 0);

                var program = new TextBlock
                {
                    Text = r.Program,
                    FontSize = 13,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(program, 1);

                var (riskColor, riskBg) = RiskColors(r.Risk);
                var riskBadge = new Border
                {
                    Background = riskBg,
                    BorderBrush = riskColor,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = r.Risk,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = riskColor
                    }
                };
                Grid.SetColumn(riskBadge, 2);

                var score = new TextBlock
                {
                    Text = r.AutoScore.HasValue ? $"{r.AutoScore.Value:0.##}" : "—",
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(score, 3);

                var verScore = new TextBlock
                {
                    Text = "—",
                    FontSize = 13,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(verScore, 4);

                var (statusColor, statusBg) = StatusColors(r.Status);
                var statusBadge = new Border
                {
                    Background = statusBg,
                    BorderBrush = statusColor,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = StatusLabel(r.Status),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = statusColor
                    }
                };
                Grid.SetColumn(statusBadge, 5);

                grid.Children.Add(namePanel);
                grid.Children.Add(program);
                grid.Children.Add(riskBadge);
                grid.Children.Add(score);
                grid.Children.Add(verScore);
                grid.Children.Add(statusBadge);

                var row = new Border
                {
                    BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(16, 12, 16, 12),
                    Child = grid
                };

                StudentRowsPanel.Children.Add(row);
            }
        }

        private static string StatusLabel(string status) => status switch
        {
            "in_progress" => "In Progress",
            "submitted" => "Pending Review",
            "graded" => "Finalized",
            "abandoned" => "Abandoned",
            _ => status
        };

        private static (System.Windows.Media.Brush fg, System.Windows.Media.Brush bg) RiskColors(string risk) => risk switch
        {
            "High" => (Brush("#C62828"), Brush("#FEF2F2")),
            "Medium" => (Brush("#B45309"), Brush("#FEF3C7")),
            _ => (Brush("#15803D"), Brush("#DCFCE7"))
        };

        private static (System.Windows.Media.Brush fg, System.Windows.Media.Brush bg) StatusColors(string status) => status switch
        {
            "in_progress" => (Brush("#1976D2"), Brush("#EFF6FF")),
            "submitted" => (Brush("#B45309"), Brush("#FEF3C7")),
            "graded" => (Brush("#15803D"), Brush("#DCFCE7")),
            "abandoned" => (Brush("#C62828"), Brush("#FEF2F2")),
            _ => (Brush("#64748B"), Brush("#F1F5F9"))
        };

        private static System.Windows.Media.Brush Brush(string hex) =>
            new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

        private void BuildScoreDistribution(ScoreDistributionDto dist)
        {
            const double maxBarWidth = 210;
            SetBar(Bar90_100, Count90_100, dist.Band90to100, dist.Max, maxBarWidth);
            SetBar(Bar80_89, Count80_89, dist.Band80to89, dist.Max, maxBarWidth);
            SetBar(Bar70_79, Count70_79, dist.Band70to79, dist.Max, maxBarWidth);
            SetBar(BarBelow70, CountBelow70, dist.BelowSeventy, dist.Max, maxBarWidth);
        }

        private static void SetBar(Border bar, TextBlock label, int count, int max, double maxWidth)
        {
            bar.Width = max > 0 ? maxWidth * count / max : 0;
            label.Text = count.ToString();
        }
    }
}
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ACPAMS.Services;
using ACPAMS.Views.Faculty.Tabs;

namespace ACPAMS.Views.Faculty
{
    public partial class ExamScoresPage : UserControl, IRefreshableView
    {
        private readonly Guid _examId;
        private readonly ExamScoresService _service = new();
        private readonly DispatcherTimer _autoRefreshTimer;

        // Tracks how many section columns are currently in the grid, so a
        // reload can cleanly remove and rebuild them without touching the
        // fixed Student/Total/Status columns on either side.
        private int _sectionColumnCount = 0;

        public ExamScoresPage(Guid examId)
        {
            InitializeComponent();
            _examId = examId;

            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoRefreshTimer.Tick += async (s, e) => await LoadAsync();

            Loaded += ExamScoresPage_Loaded;
            Unloaded += ExamScoresPage_Unloaded;
        }

        private async void ExamScoresPage_Loaded(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer.Start();
            await LoadAsync();
        }

        private void ExamScoresPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer.Stop();
        }

        // Called by MainWindow.ShowPage on every navigation to this page.
        public async Task RefreshAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            // Defensive catch-up — grades any objective answer that slipped
            // through before this exam ever had a submission (e.g. exams
            // submitted before AutoCheckerService existed, or before this
            // page was ever opened for them).
            await new AutoCheckerService().AutoGradeExamAsync(_examId);

            var summary = await _service.GetExamScoresAsync(_examId);

            ExamTitleText.Text = summary.ExamTitle;
            TotalTestTakersValue.Text = summary.TotalTestTakers.ToString();

            if (summary.HasAverage)
            {
                AverageScoreValue.Text = $"{summary.AverageScorePercent:0.#}%";
                AverageScoreSub.Text = "Across finalized + fully-scored attempts";
            }
            else
            {
                AverageScoreValue.Text = "—";
                AverageScoreSub.Text = "No finalized scores yet";
            }

            UnfinalizedValue.Text = summary.UnfinalizedCount.ToString();
            UnfinalizedCard.Visibility = summary.UnfinalizedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            BuildSectionColumns(summary.SectionNames);

            ScoresGrid.ItemsSource = summary.Rows;
            ScoresGrid.SelectionChanged -= ScoresGrid_SelectionChanged; // avoid double-subscribing on reload
            ScoresGrid.SelectionChanged += ScoresGrid_SelectionChanged;
        }

        private async void ScoresGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScoresGrid.SelectedItem is not StudentExamScoreRowDto row) return;

            AnswerDetailHeaderText.Text = $"ANSWER DETAIL — {row.StudentName}";
            AnswerDetailList.ItemsSource = await _service.GetStudentAnswerDetailsAsync(row.AttemptId);
        }

        // Removes any previously-inserted section columns and rebuilds them
        // fresh, in between the fixed "STUDENT" (index 0) and "TOTAL" columns.
        private void BuildSectionColumns(List<string> sectionNames)
        {
            for (int i = 0; i < _sectionColumnCount; i++)
            {
                ScoresGrid.Columns.RemoveAt(1);
            }

            int insertIndex = 1;
            foreach (var sectionName in sectionNames)
            {
                var column = new DataGridTextColumn
                {
                    Header = sectionName.ToUpper(),
                    Width = 150,
                    Binding = new Binding($"SectionScoreTexts[{sectionName}]")
                };

                var elementStyle = new Style(typeof(TextBlock));
                elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
                elementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, (Brush)FindResource("Text.Primary")));

                var pendingTrigger = new DataTrigger
                {
                    Binding = new Binding($"SectionScoreTexts[{sectionName}]"),
                    Value = "Pending"
                };
                pendingTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, (Brush)FindResource("Status.Warning")));
                elementStyle.Triggers.Add(pendingTrigger);

                column.ElementStyle = elementStyle;

                ScoresGrid.Columns.Insert(insertIndex, column);
                insertIndex++;
            }

            _sectionColumnCount = sectionNames.Count;
        }

        private void ReviewInGrading_Click(object sender, RoutedEventArgs e)
        {
            // AttemptId isn't actually needed by GradingTab (it loads its own
            // queue for the whole exam), but selecting the right student once
            // there is still a manual step — this at least gets the instructor
            // to the right exam's grading screen in one click.
            MainWindow.Navigate(new GradingTab(_examId));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateBackStatic();
        }
    }
}

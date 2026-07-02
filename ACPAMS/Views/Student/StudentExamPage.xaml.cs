using ACPAMS.Models;
using ACPAMS.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ACPAMS.Views.Student
{
    public partial class StudentExamPage : UserControl
    {
        private readonly ExamService _examService;
        private List<Question> _questions = new();
        private int _currentIndex = 0;
        private DispatcherTimer _timer = new();
        private int _remainingSeconds = 0;
        private readonly HashSet<int> _answeredIndices = new();

        public StudentExamPage()
        {
            InitializeComponent();
            _examService = new ExamService();
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (SessionManager.CurrentExam == null ||
                SessionManager.CurrentAttempt == null) return;

            var exam = SessionManager.CurrentExam;
            ExamTitleText.Text = $"{exam.CourseCode} — {exam.Title?.ToUpper() ?? "EXAM"}";
            ExamSubtitleText.Text = exam.ExamName ?? string.Empty;

            // ExamId is Guid (not nullable) — no .Value needed
            _questions = await _examService.GetQuestionsAsync(exam.ExamId);

            if (_questions.Count == 0)
            {
                MessageBox.Show("This exam has no questions yet.",
                    "No Questions", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int duration = exam.DurationMinutes ?? 60;
            _remainingSeconds = duration * 60;
            StartTimer();
            UpdateTimerDisplay();
            BuildNavPane();
            LoadQuestion(0);
        }

        // ── TIMER ───────────────────────────────────────────────────────
        private void StartTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _remainingSeconds--;
            UpdateTimerDisplay();

            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                MessageBox.Show("Time is up! Your exam will be submitted automatically.",
                    "Time Up", MessageBoxButton.OK, MessageBoxImage.Warning);
                _ = SubmitExamAsync();
            }
        }

        private void UpdateTimerDisplay()
        {
            int minutes = _remainingSeconds / 60;
            int seconds = _remainingSeconds % 60;
            TimerText.Text = $"{minutes:D2}:{seconds:D2}";

            if (_remainingSeconds <= 300)
            {
                TimerBorder.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                TimerText.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }
        }

        // ── NAV PANE ────────────────────────────────────────────────────
        private void BuildNavPane()
        {
            NavItemsPanel.Children.Clear();

            for (int i = 0; i < _questions.Count; i++)
            {
                int index = i;

                var dot = new Border
                {
                    Width = 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var label = new TextBlock
                {
                    Text = $"Question {i + 1}",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
                };

                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add(dot);
                panel.Children.Add(label);

                var btn = new Button
                {
                    Style = i == 0
                        ? (Style)FindResource("Btn.NavItem.Active")
                        : (Style)FindResource("Btn.NavItem"),
                    Content = panel
                };

                btn.Click += (s, e) =>
                {
                    _ = SaveCurrentAnswer();
                    LoadQuestion(index);
                };

                NavItemsPanel.Children.Add(btn);
            }

            UpdateProgress();
        }

        private void UpdateNavPane()
        {
            for (int i = 0; i < NavItemsPanel.Children.Count; i++)
            {
                if (NavItemsPanel.Children[i] is not Button btn) continue;

                btn.Style = i == _currentIndex
                    ? (Style)FindResource("Btn.NavItem.Active")
                    : (Style)FindResource("Btn.NavItem");

                if (btn.Content is StackPanel panel &&
                    panel.Children[0] is Border dot)
                {
                    dot.Background = i == _currentIndex
                        ? new SolidColorBrush(Color.FromRgb(25, 118, 210))
                        : _answeredIndices.Contains(i)
                            ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
                            : new SolidColorBrush(Color.FromRgb(203, 213, 225));
                }
            }
        }

        private void UpdateProgress()
        {
            int answered = _answeredIndices.Count;
            int total = _questions.Count;
            ProgressText.Text = $"{answered} / {total}";
            ProgressBar.Width = total > 0 ? (double)answered / total * 100 : 0;
        }

        // ── QUESTION LOADING ────────────────────────────────────────────
        private async void LoadQuestion(int index)
        {
            if (index < 0 || index >= _questions.Count) return;

            _currentIndex = index;
            var question = _questions[index];

            var existingAnswer = SessionManager.CurrentAttempt != null
                ? await _examService.GetAnswerAsync(
                    SessionManager.CurrentAttempt.AttemptId,
                    question.QuestionId)
                : null;

            UserControl canvas = question.QuestionType switch
            {
                "identification" or "essay" =>
                    new Canvas.TextInputCanvas(question, existingAnswer),
                "multiple_choice" or "true_false" =>
                    new Canvas.OptionSelectorCanvas(question, existingAnswer),
                "enumeration" =>
                    new Canvas.EnumerationCanvas(question, existingAnswer),
                "coding" =>
                    new Canvas.CodingCanvas(question, existingAnswer),
                _ => new Canvas.TextInputCanvas(question, existingAnswer)
            };

            ExamCanvasHost.Content = canvas;
            PreviousBtn.IsEnabled = index > 0;
            NextBtn.IsEnabled = index < _questions.Count - 1;
            UpdateNavPane();
        }

        // ── ANSWER SAVING ───────────────────────────────────────────────
        private async System.Threading.Tasks.Task SaveCurrentAnswer()
        {
            if (SessionManager.CurrentAttempt == null) return;
            if (ExamCanvasHost.Content is not UserControl canvas) return;

            var question = _questions[_currentIndex];

            string? answerText = null;
            string? sourceCode = null;
            string? languageUsed = null;

            if (canvas is Canvas.TextInputCanvas tc)
                answerText = tc.GetAnswer();
            else if (canvas is Canvas.OptionSelectorCanvas oc)
                answerText = oc.GetAnswer();
            else if (canvas is Canvas.EnumerationCanvas ec)
                answerText = ec.GetAnswer();
            else if (canvas is Canvas.CodingCanvas cc)
            {
                sourceCode = cc.GetSourceCode();
                languageUsed = cc.GetLanguage();
            }

            bool hasAnswer = !string.IsNullOrWhiteSpace(answerText)
                          || !string.IsNullOrWhiteSpace(sourceCode);
            if (hasAnswer) _answeredIndices.Add(_currentIndex);
            else _answeredIndices.Remove(_currentIndex);

            await _examService.SaveAnswerAsync(
                SessionManager.CurrentAttempt.AttemptId,
                question.QuestionId,
                answerText, sourceCode, languageUsed);

            UpdateProgress();
            UpdateNavPane();
        }

        // ── BUTTON EVENTS ────────────────────────────────────────────────
        private async void PreviousBtn_Click(object sender, RoutedEventArgs e)
        {
            await SaveCurrentAnswer();
            LoadQuestion(_currentIndex - 1);
        }

        private async void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            await SaveCurrentAnswer();
            LoadQuestion(_currentIndex + 1);
        }

        private async void SubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to submit your exam?\n\nThis action cannot be undone.",
                "Submit Exam", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;
            await SubmitExamAsync();
        }

        private void AlertDismiss_Click(object sender, RoutedEventArgs e)
        {
            AlertCard.Visibility = Visibility.Collapsed;
        }

        // ── SUBMIT ──────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task SubmitExamAsync()
        {
            _timer.Stop();
            await SaveCurrentAnswer();

            if (SessionManager.CurrentAttempt != null)
                await _examService.SubmitExamAsync(
                    SessionManager.CurrentAttempt.AttemptId);

            SessionManager.CurrentAttempt = null;
            SessionManager.CurrentExam = null;

            MessageBox.Show(
                "Your exam has been submitted successfully. Good luck!",
                "Submitted", MessageBoxButton.OK, MessageBoxImage.Information);

            ACPAMS.MainWindow.Resize(520, 480);
            ACPAMS.MainWindow.Navigate(new ExamEntryPage());
        }
    }
}

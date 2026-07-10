using ACPAMS.Models;
using ACPAMS.Services;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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

        // ── Monitoring ───────────────────────────────────────────────────
        private MonitoringService? _monitoring;
        private nint _keyboardHookHandle = nint.Zero;
        private NativeMethods.LowLevelKeyboardProc? _keyboardProc;

        public StudentExamPage()
        {
            InitializeComponent();
            _examService = new ExamService();
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (SessionManager.CurrentExam == null ||
                SessionManager.CurrentAttempt == null) return;

            var exam = SessionManager.CurrentExam;
            ExamTitleText.Text = $"{exam.CourseCode} — {exam.Title?.ToUpper() ?? "EXAM"}";
            ExamSubtitleText.Text = exam.ExamName ?? string.Empty;

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

            // Start monitoring
            InitializeMonitoring();
            SetupKeyboardHook();
            SetupClipboardInterception();
            SetupWindowFocusMonitoring();
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            StopMonitoring();
        }

        // ── MONITORING SETUP ─────────────────────────────────────────────
        private void InitializeMonitoring()
        {
            _monitoring = new MonitoringService(Dispatcher);
            _monitoring.FrameReady += OnFrameReady;
            _monitoring.SuspiciousActivityDetected += OnSuspiciousActivity;
            _monitoring.CheatingScoreUpdated += score =>
            {
                CheatingScoreText.Text = $"Risk Score: {score:F1} / 100";
                CheatingScoreBar.Width = (score / 100.0) * ScoreBarContainer.ActualWidth;

                var color = score switch
                {
                    < 30 => System.Windows.Media.Color.FromRgb(21, 128, 61),
                    < 60 => System.Windows.Media.Color.FromRgb(180, 83, 9),
                    _ => System.Windows.Media.Color.FromRgb(198, 40, 40)
                };
                CheatingScoreBar.Background = new System.Windows.Media.SolidColorBrush(color);

                // Notify when score reaches 100
                if (score >= 100)
                {
                    ShowAlert("Maximum Risk Score Reached",
                        "Your cheating risk score has reached 100. This incident has been recorded and will be reviewed by your instructor.");
                }
            };

            var attemptId = SessionManager.CurrentAttempt?.AttemptId ?? Guid.Empty;
            bool initialized = _monitoring.Initialize(attemptId);

            if (initialized)
            {
                _monitoring.Start();
                WebcamStatusDot.Fill = new SolidColorBrush(Color.FromRgb(21, 128, 61));
                WebcamStatusText.Text = "Webcam active";
                WebcamPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                WebcamStatusDot.Fill = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                WebcamStatusText.Text = "Webcam unavailable";
            }
        }

        private void OnFrameReady(System.Windows.Media.Imaging.BitmapSource frame)
        {
            WebcamFeed.Source = frame;
        }

        private void OnSuspiciousActivity(SuspiciousEventType type, string description)
        {
            // Only show alerts for these specific events
            if (type == SuspiciousEventType.ClipboardAttempt ||
                type == SuspiciousEventType.AltTabAttempt ||
                type == SuspiciousEventType.WindowFocusLost)
            {
                ShowAlert(type switch
                {
                    SuspiciousEventType.ClipboardAttempt => "Clipboard Attempt",
                    SuspiciousEventType.AltTabAttempt => "Alt+Tab Blocked",
                    SuspiciousEventType.WindowFocusLost => "Window Focus Lost",
                    _ => "Warning"
                }, description);
                return;
            }

            // For score-based events, only show at thresholds
            if (type == SuspiciousEventType.NoFaceDetected &&
                (description.Contains("Sustained") ||
                 description.Contains("High") ||
                 description.Contains("Critical")))
            {
                ShowAlert("High Risk Detected", description);
            }
        }
        private void ShowAlert(string title, string message)
        {
            AlertTitle.Text = title;
            AlertMessage.Text = message;
            AlertCard.Visibility = Visibility.Visible;
        }

        private void StopMonitoring()
        {
            _monitoring?.Stop();
            _monitoring?.Dispose();

            if (_keyboardHookHandle != nint.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = nint.Zero;
            }
        }

        // ── KEYBOARD HOOK (Alt+Tab interception) ─────────────────────────
        private void SetupKeyboardHook()
        {
            _keyboardProc = KeyboardHookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
                13, // WH_KEYBOARD_LL
                _keyboardProc,
                NativeMethods.GetModuleHandle(curModule.ModuleName),
                0);

            AltTabStatusDot.Fill = new SolidColorBrush(Color.FromRgb(21, 128, 61));
            AltTabStatusText.Text = "Alt+Tab intercepted";
        }

        private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool altDown = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;

                // Alt+Tab = VK_TAB (9) with Alt held
                if (vkCode == 9 && altDown)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _monitoring?.OnAltTabAttempt();
                        ShowAlert("Alt+Tab Blocked", "Switching windows is not allowed during the exam.");
                    });
                    return 1; // Block the key
                }

                // Win key (VK_LWIN=91, VK_RWIN=92)
                if (vkCode == 91 || vkCode == 92)
                {
                    return 1; // Block Win key
                }
            }
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        // ── CLIPBOARD INTERCEPTION ────────────────────────────────────────
        private void SetupClipboardInterception()
        {
            // Intercept Ctrl+C and Ctrl+V at the UserControl level
            this.AddHandler(UIElement.KeyDownEvent,
                new KeyEventHandler(OnKeyDown_ClipboardCheck), true);

            ClipboardStatusDot.Fill = new SolidColorBrush(Color.FromRgb(21, 128, 61));
            ClipboardStatusText.Text = "Clipboard blocked";
        }

        private void OnKeyDown_ClipboardCheck(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (ctrl && (e.Key == Key.C || e.Key == Key.X))
            {
                e.Handled = true;
                _monitoring?.OnClipboardAttempt();
                ShowAlert("Clipboard Blocked", "Copying content is not allowed during the exam.");
            }

            if (ctrl && e.Key == Key.V)
            {
                // Allow paste only in CodingCanvas
                if (ExamCanvasHost.Content is not Canvas.CodingCanvas)
                {
                    e.Handled = true;
                    _monitoring?.OnClipboardAttempt();
                    ShowAlert("Clipboard Blocked", "Pasting content is not allowed during the exam.");
                }
            }
        }

        // ── WINDOW FOCUS MONITORING ───────────────────────────────────────
        private void SetupWindowFocusMonitoring()
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.Deactivated += OnWindowDeactivated;
            }
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            _monitoring?.OnWindowFocusLost();
            ShowAlert("Window Focus Lost", "Please keep the exam window in focus at all times.");
        }

        // ── TIMER ─────────────────────────────────────────────────────────
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

        // ── NAV PANE ──────────────────────────────────────────────────────
        private void BuildNavPane()
        {
            NavItemsPanel.Children.Clear();

            var sections = _questions
                .GroupBy(q => q.SectionName ?? "General")
                .ToList();

            int globalIndex = 0;

            foreach (var section in sections)
            {
                // Collapsible section container
                var sectionPanel = new StackPanel();

                // Section toggle button
                var sectionBtn = new Button
                {
                    Style = (Style)FindResource("Btn.NavSection"),
                    Margin = new Thickness(0, 4, 0, 0)
                };

                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var arrow = new TextBlock
                {
                    Text = "▾",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var sectionLabel = new TextBlock
                {
                    Text = section.Key.ToUpper(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                headerPanel.Children.Add(arrow);
                headerPanel.Children.Add(sectionLabel);
                sectionBtn.Content = headerPanel;

                // Questions container (collapsible)
                var questionsPanel = new StackPanel();

                // Wire toggle
                sectionBtn.Click += (s, e) =>
                {
                    bool isCollapsed = questionsPanel.Visibility == Visibility.Collapsed;
                    questionsPanel.Visibility = isCollapsed
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    arrow.Text = isCollapsed ? "▾" : "▸";
                };

                NavItemsPanel.Children.Add(sectionBtn);
                NavItemsPanel.Children.Add(questionsPanel);

                // Add questions into questionsPanel
                foreach (var question in section)
                {
                    int index = globalIndex;
                    var dot = new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var label = new TextBlock
                    {
                        Text = $"Question {index + 1}",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children = { dot, label }
                    };

                    var btn = new Button
                    {
                        Style = index == 0
                            ? (Style)FindResource("Btn.NavItem.Active")
                            : (Style)FindResource("Btn.NavItem"),
                        Content = panel,
                        Tag = index
                    };

                    btn.Click += async (s, e) =>
                    {
                        _ = SaveCurrentAnswer();
                        LoadQuestion(index);
                    };

                    questionsPanel.Children.Add(btn);
                    globalIndex++;
                }
            }
        }

        private void UpdateNavPane()
        {
            int questionIndex = 0;
            foreach (var child in NavItemsPanel.Children)
            {
                // Skip section toggle buttons
                if (child is Button) continue;

                if (child is StackPanel questionsPanel)
                {
                    foreach (var qChild in questionsPanel.Children)
                    {
                        if (qChild is not Button btn) continue;

                        btn.Style = questionIndex == _currentIndex
                            ? (Style)FindResource("Btn.NavItem.Active")
                            : (Style)FindResource("Btn.NavItem");

                        if (btn.Content is StackPanel panel &&
                            panel.Children.Count > 0 &&
                            panel.Children[0] is Ellipse dot)
                        {
                            dot.Fill = _answeredIndices.Contains(questionIndex)
                                ? new SolidColorBrush(Color.FromRgb(25, 118, 210))
                                : new SolidColorBrush(Color.FromRgb(203, 213, 225));
                        }

                        questionIndex++;
                    }
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

        // ── QUESTION LOADING ──────────────────────────────────────────────
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

        // ── ANSWER SAVING ─────────────────────────────────────────────────
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

        // ── BUTTON EVENTS ─────────────────────────────────────────────────
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

        // ── SUBMIT ────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task SubmitExamAsync()
        {
            _timer.Stop();
            StopMonitoring();
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

    // ── P/Invoke for keyboard hook ────────────────────────────────────────
    internal static class NativeMethods
    {
        public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            nint hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern nint GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }
}
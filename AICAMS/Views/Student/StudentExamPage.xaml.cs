using ACPAMS.Data;
using ACPAMS.Models;
using ACPAMS.Services;
using ACPAMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        private List<ExamQuestionViewModel> _questionViewModels = new();
        public ExamQuestionViewModel? CurrentQuestion => _currentIndex >= 0 && _currentIndex < _questionViewModels.Count ? _questionViewModels[_currentIndex] : null;

        private int _currentIndex = 0;
        private DispatcherTimer _timer = new();
        private DispatcherTimer? _autoSaveTimer;
        private int _remainingSeconds = 0;
        private readonly HashSet<int> _answeredIndices = new();

        // ── Submission Guard ──────────────────────────────────────────────
        private bool _isSubmissionPromptActive = false;
        private bool _isExamSubmitted = false;

        // ── Kiosk Proctor Warning Polling ─────────────────────────────────
        private DispatcherTimer? _warningPollTimer;
        private bool _isPollingWarnings = false;
        private DateTime _sessionStartTime = DateTime.UtcNow;

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
            ExamTitleText.Text = $"{exam.CourseCode} — {exam.ExamName?.ToUpper() ?? "EXAM"}";
            ExamSubtitleText.Text = !string.IsNullOrEmpty(exam.ExamCode) ? $"Code: {exam.ExamCode}" : string.Empty;

            _questions = await _examService.GetQuestionsAsync(exam.ExamId, SessionManager.CurrentAttempt.AttemptId);

            if (_questions.Count == 0)
            {
                MessageBox.Show("This exam has no questions yet.",
                    "No Questions", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _questionViewModels = _questions.Select((q, i) => new ExamQuestionViewModel(q, i)).ToList();
            DataContext = this;

            int totalDurationMinutes = (exam.DurationMinutes ?? 60) + (SessionManager.CurrentAttempt?.ExtraTimeMinutes ?? 0);
            if (SessionManager.CurrentAttempt?.StartedAt != null)
            {
                var deadline = SessionManager.CurrentAttempt.StartedAt.Value.AddMinutes(totalDurationMinutes);
                _remainingSeconds = Math.Max(0, (int)(deadline - DateTime.Now).TotalSeconds);
            }
            else
            {
                _remainingSeconds = totalDurationMinutes * 60;
            }

            StartTimer();
            UpdateTimerDisplay();
            BuildNavPane();
            UpdateProgress();
            LoadQuestion(0);

            // Debounced auto-save every 20 seconds
            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _autoSaveTimer.Tick += async (s, e) => await SaveCurrentAnswer();
            _autoSaveTimer.Start();

            // Start monitoring
            InitializeMonitoring();
            SetupKeyboardHook();
            SetupClipboardInterception();
            SetupWindowFocusMonitoring();
            StartWarningPolling();
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer = null;
            StopMonitoring();
            ACPAMS.MainWindow.ExitKioskFullscreenStatic();
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

            _monitoring.EyeStatusUpdated += (ear, isOpen) =>
            {
                EyeStatusDot.Fill = isOpen
                    ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
                    : new SolidColorBrush(Color.FromRgb(180, 83, 9));
                EyeStatusText.Text = $"Eyes: {(isOpen ? "Open" : "Closed")} (EAR: {ear:F2})";
            };

            _monitoring.GazeDirectionUpdated += direction =>
            {
                var (color, text) = direction switch
                {
                    "Center" => (Color.FromRgb(21, 128, 61), "Gaze: Center"),
                    "Left" => (Color.FromRgb(198, 40, 40), "Gaze: Looking Left"),
                    "Right" => (Color.FromRgb(198, 40, 40), "Gaze: Looking Right"),
                    "Closed" => (Color.FromRgb(180, 83, 9), "Gaze: Closed"),
                    _ => (Color.FromRgb(100, 116, 139), $"Gaze: {direction}")
                };
                GazeStatusDot.Fill = new SolidColorBrush(color);
                GazeStatusText.Text = text;
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

        // ── Camera Zoom and Pan Controls ──────────────────────────────────
        private double _cameraZoom = 1.0;
        private double _panX = 0.0;
        private double _panY = 0.0;
        private Point? _lastCameraMousePos;
        private bool _isCameraPanning;

        private void UpdateCameraTransforms()
        {
            if (CameraScaleTransform != null)
            {
                CameraScaleTransform.ScaleX = _cameraZoom;
                CameraScaleTransform.ScaleY = _cameraZoom;
            }
            if (CameraTranslateTransform != null)
            {
                CameraTranslateTransform.X = _panX;
                CameraTranslateTransform.Y = _panY;
            }
            if (ZoomPercentText != null)
            {
                ZoomPercentText.Text = $"{Math.Round(_cameraZoom * 100):F0}%";
            }
        }

        private void SetZoom(double zoom)
        {
            _cameraZoom = Math.Clamp(Math.Round(zoom, 2), 1.0, 4.0);
            if (_cameraZoom <= 1.0)
            {
                _panX = 0;
                _panY = 0;
            }
            ClampCameraPan();
            UpdateCameraTransforms();
        }

        private void ClampCameraPan()
        {
            if (_cameraZoom <= 1.0)
            {
                _panX = 0;
                _panY = 0;
                return;
            }

            double width = CameraHostBorder?.ActualWidth > 0 ? CameraHostBorder.ActualWidth : 140;
            double height = CameraHostBorder?.ActualHeight > 0 ? CameraHostBorder.ActualHeight : 140;

            double maxPanX = ((_cameraZoom - 1.0) * width) / 2.0;
            double maxPanY = ((_cameraZoom - 1.0) * height) / 2.0;

            _panX = Math.Clamp(_panX, -maxPanX, maxPanX);
            _panY = Math.Clamp(_panY, -maxPanY, maxPanY);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_cameraZoom + 0.25);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(_cameraZoom - 0.25);
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(1.0);
        }

        private void CameraHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double delta = e.Delta > 0 ? 0.2 : -0.2;
            SetZoom(_cameraZoom + delta);
        }

        private void CameraHost_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_cameraZoom > 1.0 && (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed))
            {
                if (sender is FrameworkElement host)
                {
                    _isCameraPanning = true;
                    _lastCameraMousePos = e.GetPosition(host);
                    host.CaptureMouse();
                    host.Cursor = Cursors.SizeAll;
                    e.Handled = true;
                }
            }
        }

        private void CameraHost_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isCameraPanning && _lastCameraMousePos.HasValue && sender is FrameworkElement host)
            {
                Point currentPos = e.GetPosition(host);
                Vector diff = currentPos - _lastCameraMousePos.Value;

                _panX += diff.X;
                _panY += diff.Y;

                ClampCameraPan();
                UpdateCameraTransforms();

                _lastCameraMousePos = currentPos;
                e.Handled = true;
            }
        }

        private void CameraHost_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isCameraPanning && sender is FrameworkElement host)
            {
                _isCameraPanning = false;
                _lastCameraMousePos = null;
                host.ReleaseMouseCapture();
                host.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private void OnSuspiciousActivity(SuspiciousEventType type, string description)
        {
            // Only show alerts for these specific events
            if (type == SuspiciousEventType.ClipboardAttempt ||
                type == SuspiciousEventType.AltTabAttempt ||
                type == SuspiciousEventType.WindowFocusLost ||
                type == SuspiciousEventType.GazeDiverted)
            {
                ShowAlert(type switch
                {
                    SuspiciousEventType.ClipboardAttempt => "Clipboard Attempt",
                    SuspiciousEventType.AltTabAttempt => "Alt+Tab Blocked",
                    SuspiciousEventType.WindowFocusLost => "Window Focus Lost",
                    SuspiciousEventType.GazeDiverted => "Gaze Diverted",
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
            StopWarningPolling();

            if (_monitoring != null)
            {
                _monitoring.Stop();
                _monitoring.Dispose();
                _monitoring = null;
            }

            if (_keyboardHookHandle != nint.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = nint.Zero;
            }
        }

        // ── KIOSK PROCTOR WARNINGS ────────────────────────────────────────
        private void StartWarningPolling()
        {
            _sessionStartTime = DateTime.UtcNow.AddMinutes(-2);
            _warningPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _warningPollTimer.Tick += async (s, e) => await PollKioskWarningsAsync();
            _warningPollTimer.Start();
        }

        private void StopWarningPolling()
        {
            if (_warningPollTimer != null)
            {
                _warningPollTimer.Stop();
                _warningPollTimer = null;
            }
        }

        private async Task PollKioskWarningsAsync()
        {
            if (_isPollingWarnings) return;
            _isPollingWarnings = true;

            try
            {
                var currentUserId = SessionManager.CurrentUser?.UserId;
                var currentAttemptId = SessionManager.CurrentAttempt?.AttemptId;
                if (!currentUserId.HasValue) return;

                var examIdStr = SessionManager.CurrentExam?.ExamId.ToString();

                var syncResult = await Task.Run(async () =>
                {
                    using var context = new AppDbContext();
                    var query = context.Notifications
                        .Where(n => n.RecipientUserId == currentUserId.Value
                                    && n.Category == "KioskWarning"
                                    && !n.IsRead
                                    && n.CreatedAt >= _sessionStartTime);

                    if (!string.IsNullOrEmpty(examIdStr))
                    {
                        query = query.Where(n => n.TargetEntityId == null || n.TargetEntityId == examIdStr);
                    }

                    var list = await query.OrderBy(n => n.CreatedAt).ToListAsync();
                    if (list.Count > 0)
                    {
                        foreach (var item in list)
                        {
                            item.IsRead = true;
                        }
                        await context.SaveChangesAsync();
                    }

                    int? extraMinutes = null;
                    if (currentAttemptId.HasValue)
                    {
                        extraMinutes = await context.StudentExamAttempts
                            .Where(a => a.AttemptId == currentAttemptId.Value)
                            .Select(a => a.ExtraTimeMinutes)
                            .FirstOrDefaultAsync();
                    }

                    return (Warnings: list, ExtraMinutes: extraMinutes);
                });

                if (syncResult.Warnings.Count > 0)
                {
                    var latest = syncResult.Warnings.Last();
                    DisplayKioskWarning(latest.Message);
                }

                if (syncResult.ExtraMinutes.HasValue && SessionManager.CurrentAttempt != null && SessionManager.CurrentExam != null)
                {
                    int currentExtra = SessionManager.CurrentAttempt.ExtraTimeMinutes ?? 0;
                    int newExtra = syncResult.ExtraMinutes.Value;
                    if (newExtra != currentExtra)
                    {
                        SessionManager.CurrentAttempt.ExtraTimeMinutes = newExtra;
                        int duration = SessionManager.CurrentExam.DurationMinutes ?? 60;
                        var start = SessionManager.CurrentAttempt.StartedAt ?? DateTime.Now;
                        var deadline = start.AddMinutes(duration + newExtra);
                        _remainingSeconds = Math.Max(0, (int)(deadline - DateTime.Now).TotalSeconds);
                        UpdateTimerDisplay();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KioskWarningPoll] Polling error: {ex.Message}");
            }
            finally
            {
                _isPollingWarnings = false;
            }
        }

        private void DisplayKioskWarning(string message)
        {
            KioskWarningMessageText.Text = !string.IsNullOrWhiteSpace(message)
                ? message
                : "Proctor warning: Return to and focus on your exam immediately.";
            KioskWarningOverlay.Visibility = Visibility.Visible;

            ShowAlert("PROCTOR WARNING", message);

            try
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch { }
        }

        private void AcknowledgeWarning_Click(object sender, RoutedEventArgs e)
        {
            KioskWarningOverlay.Visibility = Visibility.Collapsed;
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
            if (_isSubmissionPromptActive || _isExamSubmitted) return;
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
                        Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        StrokeThickness = 1.5,
                        Fill = Brushes.Transparent,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var flag = new TextBlock
                    {
                        Text = "⚑",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Collapsed
                    };

                    var indicatorGrid = new Grid
                    {
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Children = { dot, flag }
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
                        Children = { indicatorGrid, label }
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
                        if (index == _currentIndex) return;
                        if (CheckAndPromptPrivacyWarning(async () =>
                        {
                            await SaveCurrentAnswer();
                            LoadQuestion(index);
                        }))
                        {
                            return;
                        }

                        await SaveCurrentAnswer();
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

                        var qVm = questionIndex < _questionViewModels.Count ? _questionViewModels[questionIndex] : null;

                        if (btn.Content is StackPanel panel &&
                            panel.Children.Count > 0 &&
                            panel.Children[0] is Grid indicatorGrid &&
                            indicatorGrid.Children.Count >= 2 &&
                            indicatorGrid.Children[0] is Ellipse dot &&
                            indicatorGrid.Children[1] is TextBlock flag)
                        {
                            if (qVm != null && qVm.IsFlaggedForReview)
                            {
                                flag.Visibility = Visibility.Visible;
                                dot.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                flag.Visibility = Visibility.Collapsed;
                                dot.Visibility = Visibility.Visible;

                                bool answered = (qVm != null && qVm.HasContent) || _answeredIndices.Contains(questionIndex);
                                if (answered)
                                {
                                    // Answered: Solid green circle (#10B981)
                                    var green = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                                    dot.Fill = green;
                                    dot.Stroke = green;
                                    dot.StrokeThickness = 1;
                                }
                                else
                                {
                                    // Unanswered: Muted hollow grey circle (#94A3B8)
                                    dot.Fill = Brushes.Transparent;
                                    dot.Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                                    dot.StrokeThickness = 1.5;
                                }
                            }
                        }

                        questionIndex++;
                    }
                }
            }
        }

        private void UpdateProgress()
        {
            int answered = _questionViewModels.Count > 0
                ? _questionViewModels.Count(q => q.IsAnswered)
                : _answeredIndices.Count;
            int total = _questions.Count;
            ProgressText.Text = $"{answered} / {total}";
            ProgressBar.Width = total > 0 && ProgressBarTrack.ActualWidth > 0
                ? (double)answered / total * ProgressBarTrack.ActualWidth
                : 0;
        }

        private void ProgressBarTrack_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateProgress();

        // ── QUESTION LOADING ──────────────────────────────────────────────
        private async void LoadQuestion(int index)
        {
            if (index < 0 || index >= _questions.Count) return;

            _currentIndex = index;
            var question = _questions[index];
            var qVm = _questionViewModels.Count > index ? _questionViewModels[index] : null;

            QuestionTypeBadge.Text = (question.QuestionType ?? "Question").Replace("_", " ").ToUpper();
            QuestionPointsBadge.Text = $"{question.Points ?? 1} pts";
            QuestionIndexBadge.Text = $"Question {index + 1} of {_questions.Count}";
            if (qVm != null)
            {
                MarkForReviewBtn.IsChecked = qVm.IsFlaggedForReview;
            }

            var existingAnswer = SessionManager.CurrentAttempt != null
                ? await _examService.GetAnswerAsync(
                    SessionManager.CurrentAttempt.AttemptId,
                    question.QuestionId)
                : null;

            if (existingAnswer != null && qVm != null)
            {
                if (string.IsNullOrEmpty(qVm.DraftText) && !string.IsNullOrEmpty(existingAnswer.AnswerText))
                    qVm.DraftText = existingAnswer.AnswerText;
                if (string.IsNullOrEmpty(qVm.DraftCode) && !string.IsNullOrEmpty(existingAnswer.SourceCode))
                    qVm.DraftCode = existingAnswer.SourceCode;

                if (qVm.HasContent)
                    _answeredIndices.Add(index);
            }

            UserControl canvas = question.QuestionType switch
            {
                "identification" or "essay" =>
                    new Canvas.TextInputCanvas(question, existingAnswer, index + 1, _questions.Count),
                "multiple_choice" or "true_false" =>
                    new Canvas.OptionSelectorCanvas(question, existingAnswer),
                "enumeration" =>
                    new Canvas.EnumerationCanvas(question, existingAnswer, index + 1, _questions.Count),
                "coding" =>
                    new Canvas.CodingCanvas(question, existingAnswer),
                _ => new Canvas.TextInputCanvas(question, existingAnswer, index + 1, _questions.Count)
            };

            // Display section instructions if configured for current section
            var instructions = GetSectionInstructions(question);
            if (!string.IsNullOrWhiteSpace(instructions))
            {
                SectionInstructionsText.Text = $"Instructions: {instructions.Trim()}";
                SectionInstructionsCard.Visibility = Visibility.Visible;
            }
            else
            {
                SectionInstructionsCard.Visibility = Visibility.Collapsed;
            }

            ExamCanvasHost.Content = canvas;
            PreviousBtn.IsEnabled = index > 0;
            NextBtn.IsEnabled = index < _questions.Count - 1;
            UpdateNavPane();
        }

        private void MarkForReviewBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentQuestion != null)
            {
                CurrentQuestion.IsFlaggedForReview = MarkForReviewBtn.IsChecked == true;
                UpdateNavPane();
                UpdateProgress();
            }
        }

        private string? GetSectionInstructions(Question question)
        {
            if (!string.IsNullOrWhiteSpace(question.SectionInstructions))
                return question.SectionInstructions;

            if (SessionManager.CurrentExam != null && !string.IsNullOrWhiteSpace(SessionManager.CurrentExam.Instructions))
            {
                try
                {
                    var map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(SessionManager.CurrentExam.Instructions);
                    if (map != null)
                    {
                        if (!string.IsNullOrWhiteSpace(question.SectionName) && map.TryGetValue(question.SectionName, out var inst))
                            return inst;
                        if (map.TryGetValue("General", out var genInst))
                            return genInst;
                    }
                }
                catch
                {
                    return SessionManager.CurrentExam.Instructions;
                }
            }

            return null;
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
            string? executionOutput = null;

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
                executionOutput = cc.GetExecutionOutput();
            }

            bool hasAnswer = !string.IsNullOrWhiteSpace(answerText)
                          || !string.IsNullOrWhiteSpace(sourceCode);
            if (hasAnswer) _answeredIndices.Add(_currentIndex);
            else _answeredIndices.Remove(_currentIndex);

            if (CurrentQuestion != null)
            {
                CurrentQuestion.DraftText = answerText;
                CurrentQuestion.DraftCode = sourceCode;
            }

            await _examService.SaveAnswerAsync(
                SessionManager.CurrentAttempt.AttemptId,
                question.QuestionId,
                answerText, sourceCode, languageUsed, executionOutput);

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
            if (CheckAndPromptPrivacyWarning(async () =>
            {
                await SaveCurrentAnswer();
                LoadQuestion(_currentIndex + 1);
            }))
            {
                return;
            }

            await SaveCurrentAnswer();
            LoadQuestion(_currentIndex + 1);
        }

        private async void SubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckAndPromptPrivacyWarning(async () => await ProcessSubmitConfirmationAsync()))
                return;

            await ProcessSubmitConfirmationAsync();
        }

        private async Task ProcessSubmitConfirmationAsync()
        {
            _isSubmissionPromptActive = true;
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to submit your exam?\n\nThis action cannot be undone.",
                    "Submit Exam", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    _isSubmissionPromptActive = false;
                    return;
                }

                _isExamSubmitted = true;
                await SubmitExamAsync();
            }
            catch
            {
                _isSubmissionPromptActive = false;
                throw;
            }
        }

        // ── PRE-SUBMISSION PRIVACY CHECK ──────────────────────────────────
        private Func<Task>? _pendingPrivacyAction;

        private string? GetCurrentAnswerText()
        {
            if (ExamCanvasHost.Content is not UserControl canvas) return null;

            if (canvas is Canvas.TextInputCanvas tc)
                return tc.GetAnswer();
            if (canvas is Canvas.EnumerationCanvas ec)
                return ec.GetAnswer();
            if (canvas is Canvas.CodingCanvas cc)
                return cc.GetSourceCode();

            return null;
        }

        private bool CheckAndPromptPrivacyWarning(Func<Task> onProceedAction)
        {
            string? currentAnswer = GetCurrentAnswerText();
            if (TextSanitizer.ContainsPii(currentAnswer))
            {
                _pendingPrivacyAction = onProceedAction;
                PrivacyWarningOverlay.Visibility = Visibility.Visible;
                return true;
            }
            return false;
        }

        private void PrivacyReviewBtn_Click(object sender, RoutedEventArgs e)
        {
            PrivacyWarningOverlay.Visibility = Visibility.Collapsed;
            _pendingPrivacyAction = null;
        }

        private async void PrivacyProceedBtn_Click(object sender, RoutedEventArgs e)
        {
            PrivacyWarningOverlay.Visibility = Visibility.Collapsed;
            var action = _pendingPrivacyAction;
            _pendingPrivacyAction = null;
            if (action != null)
            {
                await action();
            }
        }

        private void AlertDismiss_Click(object sender, RoutedEventArgs e)
        {
            AlertCard.Visibility = Visibility.Collapsed;
        }

        // ── SUBMIT ────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task SubmitExamAsync()
        {
            _isExamSubmitted = true;
            _timer.Stop();
            StopMonitoring();
            await SaveCurrentAnswer();

            if (SessionManager.CurrentAttempt != null)
                await _examService.SubmitExamAsync(
                    SessionManager.CurrentAttempt.AttemptId);

            SessionManager.CurrentAttempt = null;
            SessionManager.CurrentExam = null;

            ACPAMS.MainWindow.ExitKioskFullscreenStatic();
            ACPAMS.MainWindow.ClearNavigationHistory();
            ACPAMS.MainWindow.Navigate(new ExamSubmissionConfirmationPage());
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
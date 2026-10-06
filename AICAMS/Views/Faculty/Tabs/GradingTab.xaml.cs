using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty.Tabs
{
    public partial class GradingTab : UserControl, INotifyPropertyChanged, IRefreshableView
    {
        private readonly Guid? _examId;
        private readonly GradingService _service = new();
        private readonly DispatcherTimer _autoRefreshTimer;

        public ObservableCollection<SectionGroupVM> SectionGroups { get; set; } = new();
        public ObservableCollection<QuestionAnswerVM> CurrentQuestions { get; set; } = new();

        // KPI overview numbers — now backed by INotifyPropertyChanged so the
        // UI updates once the real (async) data arrives, not just at construction.
        private int _totalSubmissions;
        public int TotalSubmissions
        {
            get => _totalSubmissions;
            set { _totalSubmissions = value; OnPropertyChanged(); }
        }

        private int _aiAutoGraded;
        public int AiAutoGraded
        {
            get => _aiAutoGraded;
            set { _aiAutoGraded = value; OnPropertyChanged(); }
        }

        private int _pendingReview;
        public int PendingReview
        {
            get => _pendingReview;
            set { _pendingReview = value; OnPropertyChanged(); }
        }

        private int _finalized;
        public int Finalized
        {
            get => _finalized;
            set { _finalized = value; OnPropertyChanged(); }
        }

        public decimal CurrentAttemptTotalScore => CurrentQuestions.Sum(q => q.ObtainedPoints ?? 0);
        public decimal CurrentAttemptMaxScore => CurrentQuestions.Sum(q => q.MaxPoints);

        public void RecalculateCurrentAttemptTotals()
        {
            OnPropertyChanged(nameof(CurrentAttemptTotalScore));
            OnPropertyChanged(nameof(CurrentAttemptMaxScore));
        }

        private AttemptQueueItemVM _selectedAttempt;

        // Guards LoadAsync/LoadQuestionsAsync against overlapping calls.
        // GradingTab_Loaded and RefreshAsync (called by MainWindow.ShowPage on
        // every navigation) can both fire within milliseconds of each other —
        // without this, two concurrent LoadQuestionsAsync calls could interleave
        // their Clear()+Add() sequences, producing duplicate question cards or
        // a stale (blank) result winning over the correct one.
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public GradingTab() : this(null) { }

        public GradingTab(Guid? examId)
        {
            InitializeComponent();
            DataContext = this;
            _examId = examId;

            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoRefreshTimer.Tick += async (s, e) => await RefreshQueueOnlyAsync();

            Loaded += GradingTab_Loaded;
            Unloaded += GradingTab_Unloaded;
        }

        private async void GradingTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown += Window_PreviewKeyDown;

            _autoRefreshTimer.Start();

            await LoadAsync();
        }

        private void GradingTab_Unloaded(object sender, RoutedEventArgs e)
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
                await LoadAsync(_selectedAttempt?.AttemptId);
            }
        }

        // ── Auto-refresh (every 10s): queue + KPIs ONLY ──────────────────
        // Deliberately does NOT touch CurrentQuestions/_selectedAttempt's
        // loaded detail — if it did, any points you're mid-typing in the
        // right panel would get wiped out from under you every 10 seconds.
        // New submissions and status-badge changes still show up in the
        // queue; your in-progress grading is left alone until you Save,
        // click a different student, or explicitly press Ctrl+R.
        private async Task RefreshQueueOnlyAsync()
        {
            if (!_examId.HasValue) return;

            var data = await _service.GetGradingDataAsync(_examId.Value);

            var currentSelectionId = _selectedAttempt?.AttemptId;

            SectionGroups.Clear();
            foreach (var section in data.Sections)
            {
                var groupVM = new SectionGroupVM { SectionName = section.SectionName };
                foreach (var a in section.Attempts)
                {
                    var itemVM = new AttemptQueueItemVM
                    {
                        AttemptId = a.AttemptId,
                        StudentName = a.StudentName,
                        StudentInitials = a.StudentInitials,
                        PcLabel = a.PcLabel,
                        SectionName = a.SectionName,
                        IsFullyGraded = a.IsFullyGraded,
                        IsSelected = a.AttemptId == currentSelectionId
                    };
                    groupVM.Attempts.Add(itemVM);

                    if (itemVM.IsSelected)
                        _selectedAttempt = itemVM;
                }
                SectionGroups.Add(groupVM);
            }

            TotalSubmissions = data.TotalSubmissions;
            AiAutoGraded = data.AiAutoGraded;
            PendingReview = data.PendingReview;
            Finalized = data.Finalized;
        }

        // preserveSelectionId: if provided, re-selects the same attempt after
        // reload (instead of always jumping back to the first one) so Ctrl+R
        // and post-save refreshes don't yank focus away from what you're grading.
        private async Task LoadAsync(Guid? preserveSelectionId = null)
        {
            await _loadLock.WaitAsync();
            try
            {
                await LoadAsyncCore(preserveSelectionId);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task LoadAsyncCore(Guid? preserveSelectionId)
        {
            if (!_examId.HasValue)
            {
                // No exam context — nothing to grade.
                SectionGroups.Clear();
                CurrentQuestions.Clear();
                TotalSubmissions = AiAutoGraded = PendingReview = Finalized = 0;
                return;
            }

            var data = await _service.GetGradingDataAsync(_examId.Value);

            SectionGroups.Clear();
            foreach (var section in data.Sections)
            {
                var groupVM = new SectionGroupVM { SectionName = section.SectionName };
                foreach (var a in section.Attempts)
                {
                    groupVM.Attempts.Add(new AttemptQueueItemVM
                    {
                        AttemptId = a.AttemptId,
                        StudentName = a.StudentName,
                        StudentInitials = a.StudentInitials,
                        PcLabel = a.PcLabel,
                        SectionName = a.SectionName,
                        IsFullyGraded = a.IsFullyGraded
                    });
                }
                SectionGroups.Add(groupVM);
            }

            TotalSubmissions = data.TotalSubmissions;
            AiAutoGraded = data.AiAutoGraded;
            PendingReview = data.PendingReview;
            Finalized = data.Finalized;

            var allAttempts = SectionGroups.SelectMany(g => g.Attempts).ToList();
            var toSelect = (preserveSelectionId.HasValue
                ? allAttempts.FirstOrDefault(a => a.AttemptId == preserveSelectionId.Value)
                : null) ?? allAttempts.FirstOrDefault();

            if (toSelect != null)
            {
                toSelect.IsSelected = true;
                _selectedAttempt = toSelect;
                await LoadQuestionsAsync(toSelect.AttemptId);
            }
            else
            {
                _selectedAttempt = null;
                CurrentQuestions.Clear();
            }
        }

        private async Task LoadQuestionsAsync(Guid attemptId)
        {
            CurrentQuestions.Clear();

            if (!_examId.HasValue) return;

            var questions = await _service.GetQuestionsForAttemptAsync(_examId.Value, attemptId);

            foreach (var q in questions)
            {
                var vm = new QuestionAnswerVM
                {
                    AnswerId = q.AnswerId,
                    QuestionId = q.QuestionId,
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    StudentAnswerText = q.StudentAnswerText,
                    ExpectedAnswer = q.ExpectedAnswer,
                    MatchStatus = q.MatchStatus ?? string.Empty,
                    MaxPoints = q.MaxPoints,
                    GradedBy = q.GradedBy,
                    AiOverallFeedback = q.AiOverallFeedback,
                    AiGradingFailed = q.AiGradingFailed,
                    AiGradingError = q.AiGradingError
                };
                vm.InitializePoints(q.ObtainedPoints);
                vm.OnScoreChangedAction = () => RecalculateCurrentAttemptTotals();

                foreach (var criterion in q.AiCriteriaBreakdown)
                {
                    vm.AiCriteriaBreakdown.Add(new AiCriterionBreakdownVM
                    {
                        CriteriaName = criterion.CriteriaName,
                        MatchedTier = criterion.MatchedTier,
                        AwardedScore = criterion.AwardedScore,
                        Justification = criterion.Justification
                    });
                }

                CurrentQuestions.Add(vm);
            }

            RecalculateCurrentAttemptTotals();
        }

        // Called by MainWindow.ShowPage on every navigation to this page.
        // GradingTab is normally constructed fresh per exam, but implementing
        // this keeps it correct even if a caller ever caches/reuses an instance.
        public async Task RefreshAsync()
        {
            await LoadAsync(_selectedAttempt?.AttemptId);
        }

        // Wire to each queue item Border's MouseLeftButtonUp
        private async void QueueItem_Clicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AttemptQueueItemVM vm)
            {
                if (_selectedAttempt != null) _selectedAttempt.IsSelected = false;
                _selectedAttempt = vm;
                vm.IsSelected = true;

                await LoadQuestionsAsync(vm.AttemptId);
            }
        }

        private async void SaveGrades_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAttempt == null)
            {
                MessageBox.Show("Select a student submission first.", "Grading",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var toSave = CurrentQuestions.Select(q => new GradingQuestionDto
            {
                AnswerId = q.AnswerId,
                QuestionId = q.QuestionId,
                QuestionNumber = q.QuestionNumber,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                StudentAnswerText = q.StudentAnswerText,
                ExpectedAnswer = q.ExpectedAnswer,
                MatchStatus = q.MatchStatus,
                MaxPoints = q.MaxPoints,
                ObtainedPoints = q.ObtainedPoints
            }).ToList();

            await _service.SaveGradesAsync(_selectedAttempt.AttemptId, toSave);

            var savedAttemptId = _selectedAttempt.AttemptId;

            MessageBox.Show("Grades saved.", "Grading", MessageBoxButton.OK, MessageBoxImage.Information);

            // Full reload — not just the KPI numbers — so this student's badge
            // (Pending → Graded) and every other student's status stays current,
            // in case someone else's submission changed while this tab was open.
            await LoadAsync(savedAttemptId);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private async void FinalizeGrades_Click(object sender, RoutedEventArgs e)
        {
            if (!_examId.HasValue)
            {
                MessageBox.Show("No exam context.", "Grading", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "Finalize grades for this ENTIRE exam? This locks in every fully-graded " +
                "attempt as final — including any AI-suggested scores you haven't overridden. " +
                "Make sure you've reviewed submissions you care about before continuing.",
                "Confirm Finalize", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var context = new ACPAMS.Data.AppDbContext();
                var examStatusService = new ExamStatusService(context);
                await examStatusService.FinalizeExamGradesAsync(_examId.Value);

                MessageBox.Show("Grades finalized.", "Grading", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync(_selectedAttempt?.AttemptId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finalizing grades: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RetryAiGrading_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not QuestionAnswerVM vm)
                return;

            if (_selectedAttempt == null || vm.AnswerId == Guid.Empty)
                return;

            vm.IsAiRetrying = true;

            try
            {
                var aiJobService = new AiJobService();
                await aiJobService.RetryAnswerGradingAsync(vm.AnswerId);

                // Update this question in-place without resetting scroll or wiping other answers
                if (_examId.HasValue)
                {
                    var updatedQuestions = await _service.GetQuestionsForAttemptAsync(_examId.Value, _selectedAttempt.AttemptId);
                    var updatedQ = updatedQuestions.FirstOrDefault(q => q.AnswerId == vm.AnswerId);
                    if (updatedQ != null)
                    {
                        vm.ObtainedPoints = updatedQ.ObtainedPoints;
                        vm.GradedBy = updatedQ.GradedBy;
                        vm.AiOverallFeedback = updatedQ.AiOverallFeedback;
                        vm.AiGradingFailed = updatedQ.AiGradingFailed;
                        vm.AiGradingError = updatedQ.AiGradingError;
                        vm.AiCriteriaBreakdown.Clear();
                        foreach (var criterion in updatedQ.AiCriteriaBreakdown)
                        {
                            vm.AiCriteriaBreakdown.Add(new AiCriterionBreakdownVM
                            {
                                CriteriaName = criterion.CriteriaName,
                                MatchedTier = criterion.MatchedTier,
                                AwardedScore = criterion.AwardedScore,
                                Justification = criterion.Justification
                            });
                        }
                        vm.NotifyBreakdownChanged();
                    }

                    // Update summary KPI numbers in the header
                    var data = await _service.GetGradingDataAsync(_examId.Value);
                    TotalSubmissions = data.TotalSubmissions;
                    AiAutoGraded = data.AiAutoGraded;
                    PendingReview = data.PendingReview;
                    Finalized = data.Finalized;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI grading retry failed: {ex.Message}", "Retry Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                if (_examId.HasValue)
                {
                    var updatedQuestions = await _service.GetQuestionsForAttemptAsync(_examId.Value, _selectedAttempt.AttemptId);
                    var updatedQ = updatedQuestions.FirstOrDefault(q => q.AnswerId == vm.AnswerId);
                    if (updatedQ != null)
                    {
                        vm.AiGradingFailed = updatedQ.AiGradingFailed;
                        vm.AiGradingError = updatedQ.AiGradingError;
                    }
                }
            }
            finally
            {
                vm.IsAiRetrying = false;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Explicitly return to the main hub rather than popping whatever page was visited before
            MainWindow.NavigateToFacultyHomeStatic();
        }
    }
}
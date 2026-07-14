using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty.Tabs
{
    public partial class GradingTab : UserControl, INotifyPropertyChanged
    {
        private readonly Guid? _examId;
        private readonly GradingService _service = new();

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

        private AttemptQueueItemVM _selectedAttempt;

        public GradingTab() : this(null) { }

        public GradingTab(Guid? examId)
        {
            InitializeComponent();
            DataContext = this;
            _examId = examId;

            Loaded += GradingTab_Loaded;
            Unloaded += GradingTab_Unloaded;
        }

        private async void GradingTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown += Window_PreviewKeyDown;

            await LoadAsync();
        }

        private void GradingTab_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.PreviewKeyDown -= Window_PreviewKeyDown;
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await LoadAsync(_selectedAttempt?.AttemptId);
            }
        }

        // preserveSelectionId: if provided, re-selects the same attempt after
        // reload (instead of always jumping back to the first one) so Ctrl+R
        // and post-save refreshes don't yank focus away from what you're grading.
        private async Task LoadAsync(Guid? preserveSelectionId = null)
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
                CurrentQuestions.Add(new QuestionAnswerVM
                {
                    AnswerId = q.AnswerId,
                    QuestionId = q.QuestionId,
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    StudentAnswerText = q.StudentAnswerText,
                    MaxPoints = q.MaxPoints,
                    ObtainedPoints = q.ObtainedPoints
                });
            }
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Returns to whichever page opened Grading (Dashboard, Faculty Home, etc.)
            // via MainWindow's navigation history — same pattern as MonitoringTab's Back button.
            MainWindow.NavigateBackStatic();
        }
    }
}
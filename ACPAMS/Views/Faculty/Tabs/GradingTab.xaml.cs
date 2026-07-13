using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Faculty.Tabs
{
    // FRONTEND-ONLY for now — all data below is mock/placeholder.
    // Swap LoadMockData() for a real service call once the backend for this tab is ready.
    public partial class GradingTab : UserControl
    {
        public ObservableCollection<SectionGroupVM> SectionGroups { get; set; } = new();
        public ObservableCollection<QuestionAnswerVM> CurrentQuestions { get; set; } = new();

        // KPI overview numbers — mock for now
        public int TotalSubmissions { get; set; }
        public int AiAutoGraded { get; set; }
        public int PendingReview { get; set; }
        public int Finalized { get; set; }

        private AttemptQueueItemVM _selectedAttempt;

        public GradingTab()
        {
            InitializeComponent();
            DataContext = this;
            LoadMockData();
        }

        private void LoadMockData()
        {
            var sectionA = new SectionGroupVM
            {
                SectionName = "BSCS-3A",
                Attempts = new ObservableCollection<AttemptQueueItemVM>
                {
                    new() { AttemptId = Guid.NewGuid(), StudentName = "J. Doe", StudentInitials = "JD",
                            PcLabel = "PC-04", SectionName = "BSCS-3A", IsSelected = true, IsFullyGraded = false },
                    new() { AttemptId = Guid.NewGuid(), StudentName = "A. Smith", StudentInitials = "AS",
                            PcLabel = "PC-07", SectionName = "BSCS-3A", IsFullyGraded = false },
                }
            };

            var sectionB = new SectionGroupVM
            {
                SectionName = "BSCS-3B",
                Attempts = new ObservableCollection<AttemptQueueItemVM>
                {
                    new() { AttemptId = Guid.NewGuid(), StudentName = "B. Wayne", StudentInitials = "BW",
                            PcLabel = "PC-12", SectionName = "BSCS-3B", IsFullyGraded = true },
                }
            };

            SectionGroups.Add(sectionA);
            SectionGroups.Add(sectionB);

            TotalSubmissions = 44;
            AiAutoGraded = 44;
            PendingReview = 12;
            Finalized = 32;

            _selectedAttempt = sectionA.Attempts[0];
            LoadMockQuestions();
        }

        private void LoadMockQuestions()
        {
            CurrentQuestions.Clear();
            CurrentQuestions.Add(new QuestionAnswerVM
            {
                QuestionNumber = 1,
                QuestionText = "Write a Java method that accepts a String array and returns a new array with the elements in reversed order. Your solution must handle null inputs and empty arrays gracefully.",
                QuestionType = "Coding (Java)",
                StudentAnswerText = "public String[] reverse(String[] arr) { int n = arr.length; String[] result = new String[n]; for (int i = 0; i < n; i++) result[i] = arr[n - 1 - i]; return result; }",
                MaxPoints = 100,
                ObtainedPoints = 85
            });
            CurrentQuestions.Add(new QuestionAnswerVM
            {
                QuestionNumber = 2,
                QuestionText = "Explain the difference between pass-by-value and pass-by-reference.",
                QuestionType = "Essay",
                StudentAnswerText = "Pass-by-value copies the actual value into the function, while pass-by-reference passes the memory address so changes affect the original variable.",
                MaxPoints = 20,
                ObtainedPoints = null
            });
        }

        // Wire to each queue item Border's MouseLeftButtonUp
        private void QueueItem_Clicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AttemptQueueItemVM vm)
            {
                if (_selectedAttempt != null) _selectedAttempt.IsSelected = false;
                _selectedAttempt = vm;
                vm.IsSelected = true;

                // Mock: real version reloads CurrentQuestions for the selected attempt
                LoadMockQuestions();
            }
        }

        private void SaveGrades_Click(object sender, RoutedEventArgs e)
        {
            // Frontend-only stub — no persistence yet.
            if (_selectedAttempt != null)
                _selectedAttempt.IsFullyGraded = CurrentQuestions.All(q => q.ObtainedPoints.HasValue);

            MessageBox.Show("Grades saved. (UI only — not yet wired to database)",
                "Grading", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ACPAMS.Models;
using ACPAMS.Services;

namespace ACPAMS.Views.Faculty
{
    public partial class ArchivedExamPickerModal : Window
    {
        private readonly ExamCloningService _cloningService = new();
        private List<Exam> _allArchivedExams = new();
        private Exam? _selectedExam = null;

        public Exam? ClonedExam { get; private set; }

        public class QuestionSelectableItem
        {
            public Guid QuestionId { get; set; }
            public string Text { get; set; } = string.Empty;
            public string QuestionType { get; set; } = string.Empty;
            public decimal Points { get; set; }
            public int OrderIndex { get; set; }
            public bool HasRubric { get; set; }
            public bool IsSelected { get; set; } = true;

            public string QuestionTypeDisplay => QuestionType switch
            {
                "multiple_choice" => "Multiple Choice",
                "true_false" => "True/False",
                "identification" => "Identification",
                "enumeration" => "Enumeration",
                "essay" => "Essay",
                "coding" => "Coding",
                _ => QuestionType
            };
        }

        public class ExamDisplayItem
        {
            public Guid ExamId { get; set; }
            public string CourseCode { get; set; } = string.Empty;
            public string ExamName { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
            public string ClassSchedule { get; set; } = string.Empty;
            public string AcademicYear { get; set; } = string.Empty;
            public string Semester { get; set; } = string.Empty;
            public ICollection<Question> Questions { get; set; } = new List<Question>();

            public string SectionScheduleDisplay =>
                string.Join(" • ", new[] { Section, ClassSchedule }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private ObservableCollection<QuestionSelectableItem> _questionItems = new();
        private readonly Guid? _preselectedExamId;

        public ArchivedExamPickerModal(Guid? preselectedExamId = null)
        {
            _preselectedExamId = preselectedExamId;
            InitializeComponent();
            QuestionsItemsControl.ItemsSource = _questionItems;
            Loaded += async (s, e) => await LoadArchivedExamsAsync();
        }

        private async System.Threading.Tasks.Task LoadArchivedExamsAsync()
        {
            try
            {
                var instructorId = SessionManager.CurrentInstructor?.InstructorId;
                _allArchivedExams = await _cloningService.GetArchivedExamsAsync(instructorId);
                DisplayExams(_allArchivedExams);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load archived exams: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayExams(List<Exam> exams)
        {
            var displayList = exams.Select(e => new ExamDisplayItem
            {
                ExamId = e.ExamId,
                CourseCode = e.CourseCode ?? "N/A",
                ExamName = e.ExamName ?? "Untitled Exam",
                Section = e.Section ?? string.Empty,
                ClassSchedule = e.ClassSchedule ?? string.Empty,
                AcademicYear = e.AcademicYear ?? string.Empty,
                Semester = e.Semester ?? string.Empty,
                Questions = e.Questions
            }).ToList();

            ArchivedExamsListBox.ItemsSource = displayList;

            if (_preselectedExamId.HasValue)
            {
                var match = displayList.FirstOrDefault(d => d.ExamId == _preselectedExamId.Value);
                if (match != null)
                {
                    ArchivedExamsListBox.SelectedItem = match;
                    return;
                }
            }

            if (displayList.Count > 0)
            {
                ArchivedExamsListBox.SelectedIndex = 0;
            }
        }

        private void SearchExamsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filter = SearchExamsBox.Text?.Trim().ToLower() ?? string.Empty;
            if (string.IsNullOrEmpty(filter))
            {
                DisplayExams(_allArchivedExams);
                return;
            }

            var filtered = _allArchivedExams.Where(ex =>
                (ex.CourseCode?.ToLower().Contains(filter) == true) ||
                (ex.ExamName?.ToLower().Contains(filter) == true) ||
                (ex.Section?.ToLower().Contains(filter) == true)
            ).ToList();

            DisplayExams(filtered);
        }

        private void ArchivedExamsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArchivedExamsListBox.SelectedItem is not ExamDisplayItem item)
            {
                NoSelectionPlaceholder.Visibility = Visibility.Visible;
                ExamDetailPanel.Visibility = Visibility.Collapsed;
                _selectedExam = null;
                UpdateSummary();
                return;
            }

            _selectedExam = _allArchivedExams.FirstOrDefault(x => x.ExamId == item.ExamId);
            if (_selectedExam == null) return;

            NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
            ExamDetailPanel.Visibility = Visibility.Visible;

            // Generate initial new exam code
            var datePart = DateTime.Now.ToString("MMdd");
            var randomPart = new Random().Next(1000, 9999).ToString();
            NewExamCodeBox.Text = $"{_selectedExam.CourseCode}-{datePart}-{randomPart}";
            NewSectionBox.Text = !string.IsNullOrEmpty(_selectedExam.Section) ? $"{_selectedExam.Section} (Copy)" : "BSCS 3-A";
            NewScheduleBox.Text = _selectedExam.ClassSchedule ?? "MWF 10:30AM - 12:00PM";
            NewAcademicYearBox.Text = !string.IsNullOrEmpty(_selectedExam.AcademicYear) ? _selectedExam.AcademicYear : "2026-2027";

            if (!string.IsNullOrEmpty(_selectedExam.Semester))
            {
                foreach (ComboBoxItem cbItem in NewSemesterBox.Items)
                {
                    if (string.Equals(cbItem.Content?.ToString(), _selectedExam.Semester, StringComparison.OrdinalIgnoreCase))
                    {
                        NewSemesterBox.SelectedItem = cbItem;
                        break;
                    }
                }
            }

            // Populate Questions
            _questionItems.Clear();
            int order = 1;
            foreach (var q in _selectedExam.Questions.OrderBy(x => x.OrderIndex ?? 0))
            {
                _questionItems.Add(new QuestionSelectableItem
                {
                    QuestionId = q.QuestionId,
                    Text = q.Text,
                    QuestionType = q.QuestionType,
                    Points = q.Points ?? 1,
                    OrderIndex = order++,
                    HasRubric = q.Rubric != null,
                    IsSelected = true
                });
            }

            UpdateSummary();
        }

        private void QuestionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSummary();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var q in _questionItems)
            {
                q.IsSelected = true;
            }
            QuestionsItemsControl.Items.Refresh();
            UpdateSummary();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var q in _questionItems)
            {
                q.IsSelected = false;
            }
            QuestionsItemsControl.Items.Refresh();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var selectedCount = _questionItems.Count(q => q.IsSelected);
            var totalCount = _questionItems.Count;
            SelectionCountText.Text = $"({selectedCount} of {totalCount} selected)";

            CloneBtn.IsEnabled = selectedCount > 0 && _selectedExam != null;

            if (selectedCount == 0)
            {
                FooterSummaryText.Text = "Please select at least 1 question to clone.";
            }
            else
            {
                var totalPoints = _questionItems.Where(q => q.IsSelected).Sum(q => q.Points);
                FooterSummaryText.Text = $"{selectedCount} questions selected ({totalPoints:0.#} total points). Ready to clone into draft exam.";
            }
        }

        private async void CloneBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedExam == null) return;

            var selectedIds = _questionItems.Where(q => q.IsSelected).Select(q => q.QuestionId).ToList();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Please select at least one question to clone.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newExamCode = NewExamCodeBox.Text.Trim();
            if (string.IsNullOrEmpty(newExamCode))
            {
                MessageBox.Show("Please enter an exam code for the cloned assessment.", "Missing Exam Code", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var section = NewSectionBox.Text.Trim();
            var schedule = NewScheduleBox.Text.Trim();
            var academicYear = NewAcademicYearBox.Text.Trim();
            var semester = (NewSemesterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1st Semester";

            CloneBtn.IsEnabled = false;
            CloneBtn.Content = "Cloning...";

            try
            {
                ClonedExam = await _cloningService.CloneArchivedExamAsync(
                    _selectedExam.ExamId,
                    selectedIds,
                    newExamCode,
                    section,
                    schedule,
                    academicYear,
                    semester
                );

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clone exam:\n\n{ex.Message}", "Cloning Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CloneBtn.IsEnabled = true;
                CloneBtn.Content = "Clone & Edit Questions";
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

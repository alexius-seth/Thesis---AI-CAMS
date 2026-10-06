using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ACPAMS.Services;
using ACPAMS.Models;
using ACPAMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;

namespace ACPAMS.Views.Faculty
{
    public partial class CreateExamPage : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool _isImporting;
        public bool IsImporting
        {
            get => _isImporting;
            set
            {
                if (_isImporting != value)
                {
                    _isImporting = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _importStatusMessage = "Preparing...";
        public string ImportStatusMessage
        {
            get => _importStatusMessage;
            set
            {
                if (_importStatusMessage != value)
                {
                    _importStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ImportFileCommand { get; }
        private ObservableCollection<ExamSection> _sections = new ObservableCollection<ExamSection>();
        public ObservableCollection<ExamSection> Sections => _sections;
        public ICommand MoveSectionUpCommand { get; }
        public ICommand MoveSectionDownCommand { get; }
        private ExamSection _selectedSection;
        private string _selectedQuestionType = string.Empty;
        private string _taskCategory = "Examination";

        // Store rubric data per question
        private Dictionary<string, RubricData> _rubricData = new Dictionary<string, RubricData>();

        // Supported programming languages
        private readonly string[] _supportedLanguages = new[] { "C", "Python", "Java" };

        private ExamSection? _sectionPendingDelete;

        private Guid? _editingExamId;
        private string? _editingExamName;
        private string? _editingCourseCode;
        private int? _editingTimeLimitMinutes;
        private bool? _editingRandomizeQuestions;

        public class ExamSection : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            public string Id { get; set; } = Guid.NewGuid().ToString();

            public string Name
            {
                get => _name;
                set
                {
                    if (_name != value)
                    {
                        _name = value;
                        OnPropertyChanged(nameof(Name));
                        OnPropertyChanged(nameof(SectionTitle));
                    }
                }
            }

            public string SectionTitle
            {
                get => Name;
                set => Name = value;
            }

            public int OrderIndex { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Instructions { get; set; } = string.Empty;
            public int QuestionCount => Questions.Count;
            public bool IsActive { get; set; }
            public List<QuestionItem> Questions { get; set; } = new List<QuestionItem>();

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string propertyName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class TestCaseItem
        {
            public string Input { get; set; } = string.Empty;
            public string ExpectedOutput { get; set; } = string.Empty;
        }

        public class QuestionItem
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Text { get; set; } = string.Empty;
            public string Prompt
            {
                get => Text;
                set => Text = value;
            }
            public int Points { get; set; }
            public string Type { get; set; } = string.Empty;
            public int Order { get; set; }
            public string? CorrectAnswer { get; set; }
            public string? OptionA { get; set; }
            public string? OptionB { get; set; }
            public string? OptionC { get; set; }
            public string? OptionD { get; set; }
            public string? ExpectedAnswer { get; set; }
            public bool? IsTrue { get; set; }
            public string ProgrammingLanguage { get; set; } = "C";
            public string TargetLanguage
            {
                get => ProgrammingLanguage;
                set => ProgrammingLanguage = value;
            }
            public string SectionId { get; set; } = string.Empty;
            public List<string> AcceptedAnswers { get; set; } = new List<string>();
            public string ExpectedAnswers
            {
                get => string.Join(", ", AcceptedAnswers);
                set
                {
                    if (string.IsNullOrWhiteSpace(value))
                        AcceptedAnswers = new List<string>();
                    else
                        AcceptedAnswers = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim()).ToList();
                }
            }
            public string? StarterCode { get; set; }
            public string? StarterCodeStub
            {
                get => StarterCode;
                set => StarterCode = value;
            }
            public string? Rubric { get; set; }
            public List<TestCaseItem> TestCases { get; set; } = new List<TestCaseItem>();
            public double PromptHeight { get; set; } = 80;
        }

        // Default constructor
        public CreateExamPage() : this("Examination")
        {
        }

        // Constructor with category
        public CreateExamPage(string category)
        {
            _taskCategory = category;

            MoveSectionUpCommand = new RelayCommand(
                execute: param => { if (param is ExamSection s) MoveSectionUp(s); },
                canExecute: param => param is ExamSection s && _sections.IndexOf(s) > 0
            );
            MoveSectionDownCommand = new RelayCommand(
                execute: param => { if (param is ExamSection s) MoveSectionDown(s); },
                canExecute: param => param is ExamSection s && _sections.IndexOf(s) >= 0 && _sections.IndexOf(s) < _sections.Count - 1
            );
            ImportFileCommand = new RelayCommand(
                execute: async param => await ExecuteImportFileAsync(param as string),
                canExecute: _ => !IsImporting
            );

            InitializeComponent();

            _sections = new ObservableCollection<ExamSection>();
            _selectedSection = null;
            _selectedQuestionType = string.Empty;

            UpdateSectionList();
            UpdateQuestionsDisplay();
            UpdateActiveSectionHeader();
            ResetAllTypeButtons();
            UpdateHeaderWithCategory();
        }

        public void MoveSectionUp(ExamSection section)
        {
            if (section == null) return;
            int index = _sections.IndexOf(section);
            if (index > 0)
            {
                _sections.Move(index, index - 1);
                UpdateSectionOrderIndices();
                UpdateSectionList();
            }
        }

        public void MoveSectionDown(ExamSection section)
        {
            if (section == null) return;
            int index = _sections.IndexOf(section);
            if (index >= 0 && index < _sections.Count - 1)
            {
                _sections.Move(index, index + 1);
                UpdateSectionOrderIndices();
                UpdateSectionList();
            }
        }

        private void UpdateSectionOrderIndices()
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                _sections[i].OrderIndex = i + 1;
            }
        }

        // Constructor for editing existing exam
        public CreateExamPage(Guid examId) : this("Examination")
        {
            _editingExamId = examId;
            Loaded += async (s, e) => await LoadExistingExamAsync(examId);
        }

        private async Task LoadExistingExamAsync(Guid examId)
        {
            try
            {
                using var dbContext = new ACPAMS.Data.AppDbContext();
                var exam = await dbContext.Exams
                    .Include(e => e.Questions)
                        .ThenInclude(q => q.Options)
                    .Include(e => e.Questions)
                        .ThenInclude(q => q.Rubric)
                            .ThenInclude(r => r.Criteria)
                                .ThenInclude(c => c.Levels)
                    .FirstOrDefaultAsync(e => e.ExamId == examId);

                if (exam == null)
                {
                    ToastNotification.Show("Exam not found.", Application.Current.MainWindow, 3, true);
                    return;
                }

                _editingExamId = exam.ExamId;
                _editingExamName = exam.ExamName;
                _editingCourseCode = exam.CourseCode;
                _editingTimeLimitMinutes = exam.DurationMinutes;
                _editingRandomizeQuestions = exam.RandomizeQuestions;

                _sections.Clear();
                _rubricData.Clear();

                var grouped = exam.Questions
                    .GroupBy(q => string.IsNullOrWhiteSpace(q.SectionName) ? "General" : q.SectionName)
                    .OrderBy(g => g.Min(q => q.OrderIndex ?? 0))
                    .ToList();

                foreach (var g in grouped)
                {
                    var firstQ = g.FirstOrDefault();
                    var sectionType = firstQ != null ? GetProperTypeName(firstQ.QuestionType) : "Identification";
                    var sec = new ExamSection
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = g.Key,
                        Type = sectionType,
                        Instructions = string.Empty,
                        IsActive = false,
                        Questions = new List<QuestionItem>()
                    };

                    foreach (var q in g.OrderBy(x => x.OrderIndex ?? 0))
                    {
                        var properType = GetProperTypeName(q.QuestionType);
                        var qi = new QuestionItem
                        {
                            Id = q.QuestionId.ToString(),
                            Text = q.Text,
                            Points = (int)(q.Points ?? 1),
                            Type = properType,
                            Order = q.OrderIndex ?? 1,
                            CorrectAnswer = q.CorrectAnswer ?? string.Empty,
                            ExpectedAnswer = q.CorrectAnswer ?? string.Empty,
                            IsTrue = q.CorrectAnswer == "True" ? true : (q.CorrectAnswer == "False" ? false : null),
                            ProgrammingLanguage = q.ProgrammingLanguage ?? "C",
                            StarterCode = q.StarterCode,
                            SectionId = sec.Id,
                            ExpectedAnswers = q.CorrectAnswer ?? string.Empty
                        };

                        if (properType == "Multiple Choice" && q.Options != null)
                        {
                            var opts = q.Options.OrderBy(o => o.OrderIndex).ToList();
                            if (opts.Count > 0) qi.OptionA = opts[0].OptionText;
                            if (opts.Count > 1) qi.OptionB = opts[1].OptionText;
                            if (opts.Count > 2) qi.OptionC = opts[2].OptionText;
                            if (opts.Count > 3) qi.OptionD = opts[3].OptionText;
                        }

                        if (q.Rubric != null)
                        {
                            _rubricData[qi.Id] = new RubricData
                            {
                                Description = q.Rubric.Description ?? string.Empty,
                                AiInstructions = q.Rubric.AiInstructions ?? string.Empty,
                                TotalWeight = (int)(q.Rubric.TotalWeight ?? 0),
                                Criteria = q.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => new RubricCriteriaData
                                {
                                    Name = c.CriteriaName,
                                    Description = c.Description ?? string.Empty,
                                    MaxScore = (double)(c.MaxScore ?? 0),
                                    WeightPercentage = c.WeightPercentage ?? 0,
                                    Levels = c.Levels.OrderBy(l => l.OrderIndex).Select(l => new RubricLevelData
                                    {
                                        Name = l.LevelName,
                                        Points = (double)(l.ScorePercentage ?? 0),
                                        ScorePercentage = (double)(l.ScorePercentage ?? 0),
                                        Descriptor = l.Descriptor ?? string.Empty
                                    }).ToList()
                                }).ToList()
                            };
                        }

                        sec.Questions.Add(qi);
                    }

                    _sections.Add(sec);
                }

                if (_sections.Count > 0)
                {
                    _sections[0].IsActive = true;
                    _selectedSection = _sections[0];
                    SelectTypeButton(_selectedSection.Type);
                }

                UpdateSectionList();
                UpdateQuestionsDisplay();
                UpdateActiveSectionHeader();

                ToastNotification.Show($"Loaded exam: {_editingExamName}", Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load exam:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateHeaderWithCategory()
        {
            var label = FindName("CreateExamManagerLabel") as TextBlock;
            if (label != null)
            {
                string categoryDisplay = _taskCategory switch
                {
                    "Examination" => "CREATE EXAM MANAGER",
                    "Quiz" => "CREATE QUIZ MANAGER",
                    "Activity" => "CREATE ACTIVITY MANAGER",
                    _ => "CREATE EXAM MANAGER"
                };
                label.Text = categoryDisplay;
            }

            // Show the category badge
            var badge = FindName("CategoryBadge") as Border;
            var badgeText = FindName("CategoryBadgeText") as TextBlock;
            if (badge != null && badgeText != null)
            {
                badge.Visibility = Visibility.Visible;
                badgeText.Text = _taskCategory.ToUpper();

                // Change colors based on category
                switch (_taskCategory)
                {
                    case "Examination":
                        badge.Background = (Brush)FindResource("Accent.PrimaryBg");
                        badge.BorderBrush = (Brush)FindResource("Accent.Primary");
                        badgeText.Foreground = (Brush)FindResource("Accent.Primary");
                        break;
                    case "Quiz":
                        badge.Background = (Brush)FindResource("Status.SuccessBg");
                        badge.BorderBrush = (Brush)FindResource("Status.Success");
                        badgeText.Foreground = (Brush)FindResource("Status.Success");
                        break;
                    case "Activity":
                        badge.Background = (Brush)FindResource("Status.WarningBg");
                        badge.BorderBrush = (Brush)FindResource("Status.Warning");
                        badgeText.Foreground = (Brush)FindResource("Status.Warning");
                        break;
                }
            }
        }

        private void UpdateActiveSectionHeader()
        {
            var headerText = FindName("ActiveSectionHeader") as TextBlock;
            var totalDisplay = FindName("TotalQuestionsDisplay") as TextBlock;
            var typeDisplay = FindName("ActiveTypeDisplay") as TextBlock;
            var renameBtn = FindName("RenameActiveSectionBtn") as Button;

            if (_selectedSection != null)
            {
                if (headerText != null)
                    headerText.Text = $"{_selectedSection.Name} — {_selectedSection.Type}";
                if (totalDisplay != null)
                    totalDisplay.Text = $"Total: {_selectedSection.Questions.Sum(q => q.Points)} pts";
                if (typeDisplay != null)
                    typeDisplay.Text = _selectedSection.Type;
                if (renameBtn != null)
                    renameBtn.Visibility = Visibility.Visible;
            }
            else
            {
                if (headerText != null)
                    headerText.Text = "No section selected";
                if (totalDisplay != null)
                    totalDisplay.Text = "Total: 0 pts";
                if (typeDisplay != null)
                    typeDisplay.Text = "—";
                if (renameBtn != null)
                    renameBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void ActiveSectionHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && _selectedSection != null)
            {
                StartRenameActiveSection();
            }
        }

        private void RenameActiveSectionBtn_Click(object sender, RoutedEventArgs e)
        {
            StartRenameActiveSection();
        }

        private void StartRenameActiveSection()
        {
            if (_selectedSection == null) return;
            var headerText = FindName("ActiveSectionHeader") as TextBlock;
            var editBox = FindName("ActiveSectionHeaderEdit") as TextBox;
            var editBorder = FindName("ActiveSectionHeaderEditBorder") as Border;
            var renameBtn = FindName("RenameActiveSectionBtn") as Button;

            if (editBox != null && headerText != null && editBorder != null)
            {
                editBox.Text = _selectedSection.Name;
                headerText.Visibility = Visibility.Collapsed;
                if (renameBtn != null) renameBtn.Visibility = Visibility.Collapsed;
                editBorder.Visibility = Visibility.Visible;
                editBox.Focus();
                editBox.SelectAll();
            }
        }

        private void CommitRenameActiveSection()
        {
            var headerText = FindName("ActiveSectionHeader") as TextBlock;
            var editBox = FindName("ActiveSectionHeaderEdit") as TextBox;
            var editBorder = FindName("ActiveSectionHeaderEditBorder") as Border;
            var renameBtn = FindName("RenameActiveSectionBtn") as Button;

            if (editBorder != null && editBorder.Visibility == Visibility.Visible)
            {
                if (_selectedSection != null && editBox != null && !string.IsNullOrWhiteSpace(editBox.Text))
                {
                    _selectedSection.Name = editBox.Text.Trim();
                }
                editBorder.Visibility = Visibility.Collapsed;
                if (headerText != null) headerText.Visibility = Visibility.Visible;
                if (renameBtn != null && _selectedSection != null) renameBtn.Visibility = Visibility.Visible;
                UpdateActiveSectionHeader();
                UpdateSectionList();
            }
        }

        private void ActiveSectionHeaderEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                CommitRenameActiveSection();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                var headerText = FindName("ActiveSectionHeader") as TextBlock;
                var editBorder = FindName("ActiveSectionHeaderEditBorder") as Border;
                var renameBtn = FindName("RenameActiveSectionBtn") as Button;
                if (editBorder != null) editBorder.Visibility = Visibility.Collapsed;
                if (headerText != null) headerText.Visibility = Visibility.Visible;
                if (renameBtn != null && _selectedSection != null) renameBtn.Visibility = Visibility.Visible;
            }
        }

        private void ActiveSectionHeaderEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitRenameActiveSection();
        }

        private string GetProperTypeName(string type)
        {
            if (string.IsNullOrEmpty(type)) return string.Empty;

            var trimmed = type.Trim();
            var lowerType = trimmed.ToLower();

            if (lowerType.Contains("identification") || lowerType == "id" || lowerType.Contains("fill"))
                return "Identification";
            if ((lowerType.Contains("multiple") && lowerType.Contains("choice")) || lowerType == "multiplechoice" || lowerType == "mcq")
                return "Multiple Choice";
            if ((lowerType.Contains("true") && lowerType.Contains("false")) || lowerType == "trueorfalse" || lowerType == "tf" || lowerType == "t/f")
                return "True or False";
            if (lowerType.Contains("essay"))
                return "Essay";
            if (lowerType.Contains("enumeration") || lowerType == "enum")
                return "Enumeration";
            if (lowerType.Contains("coding") || lowerType == "code" || lowerType.Contains("programming") || lowerType.Contains("problem") || lowerType.Contains("hands-on") || lowerType.Contains("hands on"))
                return "Coding";

            return trimmed;
        }

        private Button GetTypeButton(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;

            var properType = GetProperTypeName(type);
            var lowerType = properType.ToLower();

            switch (lowerType)
            {
                case "identification": return AddIdentificationBtn;
                case "multiple choice": return AddMultipleChoiceBtn;
                case "true or false": return AddTrueFalseBtn;
                case "essay": return AddEssayBtn;
                case "enumeration": return AddEnumerationBtn;
                case "coding": return AddCodingBtn;
                default: return null;
            }
        }

        private async void SaveAndPublish_Click(object sender, RoutedEventArgs e)
        {
            if (_sections == null || _sections.Count == 0)
            {
                ToastNotification.Show("Please add at least one section before publishing.",
                    Application.Current.MainWindow, 4, true);
                return;
            }

            if (_selectedSection != null && _selectedSection.Questions.Count == 0)
            {
                ToastNotification.Show($"Section '{_selectedSection.Name}' has no questions. Please add questions before publishing.",
                    Application.Current.MainWindow, 4, true);
                return;
            }

            var publishModal = new PublishConfirmModal(
                _editingExamName,
                _editingCourseCode,
                _editingTimeLimitMinutes,
                _editingRandomizeQuestions);
            publishModal.Owner = Application.Current.MainWindow;

            if (publishModal.ShowDialog() == true)
            {
                try
                {
                    ToastNotification.Show("Publishing exam...", Application.Current.MainWindow, 2, false);

                    var dbContext = new ACPAMS.Data.AppDbContext();
                    var instructorId = SessionManager.CurrentInstructor?.InstructorId;

                    if (dbContext == null)
                    {
                        ToastNotification.Show("Database connection not available.", Application.Current.MainWindow, 4, true);
                        return;
                    }

                    if (string.IsNullOrEmpty(instructorId))
                    {
                        ToastNotification.Show("Instructor not found. Please login again.", Application.Current.MainWindow, 4, true);
                        return;
                    }

                    var examService = new ExamService(dbContext, instructorId);

                    // Use the rubric data collected from the AIRubricModal
                    var rubricData = _rubricData;

                    // Convert _sections to ExamService.ExamSectionData
                    var sectionsToSave = _sections.Select(s => new ExamService.ExamSectionData
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Type = s.Type,
                        Instructions = s.Instructions,
                        IsActive = s.IsActive,
                        Questions = s.Questions.Select(q => new ExamService.QuestionItemData
                        {
                            Id = q.Id,
                            Text = q.Text,
                            Points = q.Points,
                            Type = q.Type,
                            Order = q.Order,
                            CorrectAnswer = q.CorrectAnswer,
                            OptionA = q.OptionA,
                            OptionB = q.OptionB,
                            OptionC = q.OptionC,
                            OptionD = q.OptionD,
                            ExpectedAnswer = q.ExpectedAnswer,
                            IsTrue = q.IsTrue,
                            ProgrammingLanguage = q.ProgrammingLanguage,
                            StarterCode = q.StarterCode,
                            SectionId = string.IsNullOrEmpty(q.SectionId) ? s.Id : q.SectionId,
                            ExpectedAnswers = q.ExpectedAnswers
                        }).ToList()
                    }).ToList();

                    var exam = await examService.SaveExamAsync(
                        publishModal.ExamName,
                        publishModal.CourseCode,
                        publishModal.GeneratedExamCode,
                        publishModal.TimeLimitMinutes,
                        sectionsToSave,
                        rubricData,
                        instructorId,
                        publishModal.RandomizeQuestions,
                        _editingExamId
                    );

                    ToastNotification.Show($"Exam '{exam.ExamName}' published successfully!",
                        Application.Current.MainWindow, 4, false);

                    NavigateToDashboard(sender, e);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to publish exam:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                        "Publish Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectTypeButton(string type)
        {
            ResetAllTypeButtons();

            var properType = GetProperTypeName(type);

            if (string.IsNullOrEmpty(properType) || properType == type)
            {
                var lowerType = type.ToLower();
                if (lowerType.Contains("multiple") && lowerType.Contains("choice"))
                    properType = "Multiple Choice";
                else if (lowerType.Contains("true") && lowerType.Contains("false"))
                    properType = "True or False";
                else if (lowerType.Contains("identification") || lowerType == "id")
                    properType = "Identification";
                else if (lowerType.Contains("enumeration") || lowerType == "enum")
                    properType = "Enumeration";
                else if (lowerType.Contains("coding") || lowerType == "code")
                    properType = "Coding";
                else if (lowerType == "essay")
                    properType = "Essay";
            }

            Button targetButton = GetTypeButton(properType);

            if (targetButton != null)
            {
                targetButton.Background = (Brush)FindResource("Accent.PrimaryBg");
                targetButton.BorderBrush = (Brush)FindResource("Accent.Primary");
                targetButton.BorderThickness = new System.Windows.Thickness(2);
                FillTypeDot(targetButton, true);
                _selectedQuestionType = properType;
            }
        }

        private void ResetAllTypeButtons()
        {
            var typeButtons = new[]
            {
                AddIdentificationBtn,
                AddMultipleChoiceBtn,
                AddTrueFalseBtn,
                AddEssayBtn,
                AddEnumerationBtn,
                AddCodingBtn
            };

            foreach (var btn in typeButtons)
            {
                if (btn != null)
                {
                    btn.Background = (Brush)FindResource("Bg.Surface");
                    btn.BorderBrush = (Brush)FindResource("Border.Subtle");
                    btn.BorderThickness = new System.Windows.Thickness(1);
                    FillTypeDot(btn, false);
                }
            }
        }

        private void FillTypeDot(Button btn, bool isFilled)
        {
            if (btn == null) return;

            var contentStack = btn.Content as StackPanel;
            if (contentStack == null) return;

            foreach (var child in contentStack.Children)
            {
                if (child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is Ellipse ellipse)
                        {
                            UpdateEllipse(ellipse, isFilled);
                            return;
                        }
                    }
                }
                else if (child is Ellipse ellipse)
                {
                    UpdateEllipse(ellipse, isFilled);
                    return;
                }
            }
        }

        private void UpdateEllipse(Ellipse ellipse, bool isFilled)
        {
            if (ellipse == null) return;

            if (isFilled)
            {
                ellipse.Fill = (Brush)FindResource("Accent.Primary");
                ellipse.Stroke = (Brush)FindResource("Accent.Primary");
                ellipse.StrokeThickness = 1.5;
            }
            else
            {
                ellipse.Fill = System.Windows.Media.Brushes.Transparent;
                ellipse.Stroke = (Brush)FindResource("Border.Medium");
                ellipse.StrokeThickness = 1.5;
            }
        }

        private void AddSection_Click(object sender, RoutedEventArgs e)
        {
            var modal = new AddSectionModal();
            modal.Owner = Application.Current.MainWindow;

            if (modal.ShowDialog() == true)
            {
                var properType = GetProperTypeName(modal.QuestionType);

                var newSection = new ExamSection
                {
                    Name = modal.SectionName,
                    Type = properType,
                    Instructions = modal.Instructions,
                    IsActive = false,
                    Questions = new List<QuestionItem>()
                };

                _sections.Add(newSection);

                foreach (var s in _sections)
                    s.IsActive = false;

                newSection.IsActive = true;
                _selectedSection = newSection;

                SelectTypeButton(properType);

                UpdateSectionList();
                UpdateQuestionsDisplay();
                UpdateActiveSectionHeader();

                ToastNotification.Show($"Section '{newSection.Name}' added successfully!", Application.Current.MainWindow);
            }
        }

        private void UpdateSectionList()
        {
            var container = FindName("SectionsContainer") as StackPanel;
            if (container == null) return;

            container.Children.Clear();

            for (int i = 0; i < _sections.Count; i++)
            {
                var section = _sections[i];
                section.OrderIndex = i + 1;

                var button = new Button
                {
                    Style = (Style)FindResource("Btn.SectionItem"),
                    Tag = section.Id,
                    DataContext = section
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var colorBorder = new Border
                {
                    Width = 4,
                    Height = 34,
                    CornerRadius = new CornerRadius(2),
                    Background = GetTypeColor(section.Type),
                    Margin = new System.Windows.Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(colorBorder, 0);
                grid.Children.Add(colorBorder);

                var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(infoStack, 1);

                var nameText = new TextBlock
                {
                    Text = section.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Text.Primary"),
                    ToolTip = "Double-click to rename",
                    Cursor = Cursors.Hand
                };

                var renameBox = new TextBox
                {
                    Text = section.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Text.Primary"),
                    Background = Brushes.White,
                    BorderBrush = (Brush)FindResource("Accent.Primary"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4, 1, 4, 1),
                    Visibility = Visibility.Collapsed
                };

                nameText.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        e.Handled = true;
                        nameText.Visibility = Visibility.Collapsed;
                        renameBox.Visibility = Visibility.Visible;
                        renameBox.Text = section.Name;
                        renameBox.Focus();
                        renameBox.SelectAll();
                    }
                };

                void CommitInlineRename()
                {
                    if (renameBox.Visibility == Visibility.Visible)
                    {
                        string newName = renameBox.Text.Trim();
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            section.Name = newName;
                        }
                        nameText.Text = section.Name;
                        renameBox.Visibility = Visibility.Collapsed;
                        nameText.Visibility = Visibility.Visible;
                        UpdateActiveSectionHeader();
                    }
                }

                renameBox.PreviewMouseLeftButtonDown += (s, e) => e.Handled = false;
                renameBox.PreviewMouseLeftButtonUp += (s, e) => e.Handled = true;

                renameBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        CommitInlineRename();
                    }
                    else if (e.Key == Key.Escape)
                    {
                        e.Handled = true;
                        renameBox.Text = section.Name;
                        renameBox.Visibility = Visibility.Collapsed;
                        nameText.Visibility = Visibility.Visible;
                    }
                };

                renameBox.LostFocus += (s, e) => CommitInlineRename();

                infoStack.Children.Add(nameText);
                infoStack.Children.Add(renameBox);

                var typeText = new TextBlock
                {
                    Text = section.Type,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("Text.Muted")
                };
                infoStack.Children.Add(typeText);

                grid.Children.Add(infoStack);

                var rightStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(rightStack, 2);

                // Move Up Button
                var moveUpBtn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new System.Windows.Thickness(0),
                    Padding = new System.Windows.Thickness(3, 2, 3, 2),
                    Cursor = Cursors.Hand,
                    ToolTip = "Move section up",
                    Tag = section,
                    Opacity = 0.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = i > 0 ? Visibility.Visible : Visibility.Collapsed,
                    Content = new TextBlock
                    {
                        Text = "\uE74A",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 10,
                        Foreground = (Brush)FindResource("Text.Muted")
                    }
                };
                moveUpBtn.MouseEnter += (s, e) => { moveUpBtn.Opacity = 1.0; };
                moveUpBtn.MouseLeave += (s, e) => { moveUpBtn.Opacity = 0.5; };
                moveUpBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    MoveSectionUp(section);
                };
                rightStack.Children.Add(moveUpBtn);

                // Move Down Button
                var moveDownBtn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new System.Windows.Thickness(0),
                    Padding = new System.Windows.Thickness(3, 2, 3, 2),
                    Cursor = Cursors.Hand,
                    ToolTip = "Move section down",
                    Tag = section,
                    Opacity = 0.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = i < _sections.Count - 1 ? Visibility.Visible : Visibility.Collapsed,
                    Content = new TextBlock
                    {
                        Text = "\uE74B",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 10,
                        Foreground = (Brush)FindResource("Text.Muted")
                    }
                };
                moveDownBtn.MouseEnter += (s, e) => { moveDownBtn.Opacity = 1.0; };
                moveDownBtn.MouseLeave += (s, e) => { moveDownBtn.Opacity = 0.5; };
                moveDownBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    MoveSectionDown(section);
                };
                rightStack.Children.Add(moveDownBtn);

                // Rename Button
                var renameBtn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new System.Windows.Thickness(0),
                    Padding = new System.Windows.Thickness(3, 2, 3, 2),
                    Cursor = Cursors.Hand,
                    ToolTip = $"Rename {section.Name}",
                    Tag = section,
                    Opacity = 0.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = new TextBlock
                    {
                        Text = "\uE70F",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 10,
                        Foreground = (Brush)FindResource("Text.Muted")
                    }
                };
                renameBtn.MouseEnter += (s, e) => { renameBtn.Opacity = 1.0; };
                renameBtn.MouseLeave += (s, e) => { renameBtn.Opacity = 0.5; };
                renameBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    nameText.Visibility = Visibility.Collapsed;
                    renameBox.Visibility = Visibility.Visible;
                    renameBox.Text = section.Name;
                    renameBox.Focus();
                    renameBox.SelectAll();
                };
                rightStack.Children.Add(renameBtn);

                var badge = new Border
                {
                    Background = (Brush)FindResource("Bg.Base"),
                    BorderBrush = (Brush)FindResource("Border.Medium"),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new System.Windows.Thickness(7, 2, 7, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new System.Windows.Thickness(0, 0, 4, 0)
                };

                var countText = new TextBlock
                {
                    Text = section.Questions.Count.ToString(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("Text.Secondary")
                };
                badge.Child = countText;
                rightStack.Children.Add(badge);

                var deleteBtn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new System.Windows.Thickness(0),
                    Padding = new System.Windows.Thickness(4, 2, 4, 2),
                    Cursor = Cursors.Hand,
                    ToolTip = $"Delete {section.Name}",
                    Tag = section,
                    Opacity = 0.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = new TextBlock
                    {
                        Text = "\uE74D",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 11,
                        Foreground = (Brush)FindResource("Text.Muted")
                    }
                };

                deleteBtn.MouseEnter += (s, e) =>
                {
                    deleteBtn.Opacity = 1.0;
                    if (deleteBtn.Content is TextBlock tb)
                        tb.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                };
                deleteBtn.MouseLeave += (s, e) =>
                {
                    deleteBtn.Opacity = 0.5;
                    if (deleteBtn.Content is TextBlock tb)
                        tb.Foreground = (Brush)FindResource("Text.Muted");
                };
                deleteBtn.Click += DeleteSectionBtn_Click;

                rightStack.Children.Add(deleteBtn);
                grid.Children.Add(rightStack);

                button.Content = grid;
                button.Click += SectionButton_Click;

                if (section.IsActive)
                {
                    button.Style = (Style)FindResource("Btn.SectionItem.Active");
                }

                container.Children.Add(button);
            }
        }

        private void SectionButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var section = button.DataContext as ExamSection;
            if (section == null) return;

            foreach (var s in _sections)
                s.IsActive = false;

            section.IsActive = true;
            _selectedSection = section;

            SelectTypeButton(section.Type);

            UpdateSectionList();
            UpdateQuestionsDisplay();
            UpdateActiveSectionHeader();
        }

        private void UpdateQuestionsDisplay()
        {
            var container = FindName("QuestionsContainer") as StackPanel;
            if (container == null) return;

            container.Children.Clear();

            if (_selectedSection == null)
            {
                var emptyCard = new Border
                {
                    Background = (Brush)FindResource("Bg.Surface"),
                    BorderBrush = (Brush)FindResource("Border.Subtle"),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new System.Windows.Thickness(40, 36, 40, 36),
                    Margin = new System.Windows.Thickness(0, 48, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "\uE82D",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 32,
                    Foreground = (Brush)FindResource("Text.Muted"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new System.Windows.Thickness(0, 0, 0, 12)
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "No active section",
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Text.Primary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new System.Windows.Thickness(0, 0, 0, 4)
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Add a Section from the sidebar to begin.",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("Text.Secondary"),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyCard.Child = emptyStack;
                container.Children.Add(emptyCard);
                return;
            }

            foreach (var q in _selectedSection.Questions)
            {
                var cardBorder = new Border
                {
                    Style = (Style)FindResource("Card")
                };

                var accentBorder = new Border
                {
                    BorderBrush = GetTypeColor(q.Type),
                    BorderThickness = new System.Windows.Thickness(4, 0, 0, 0),
                    CornerRadius = new CornerRadius(0, 8, 8, 0)
                };

                var mainStack = new StackPanel();

                // ---- Header ----
                var headerBorder = new Border
                {
                    BorderBrush = (Brush)FindResource("Border.Subtle"),
                    BorderThickness = new System.Windows.Thickness(0, 0, 0, 1),
                    Padding = new System.Windows.Thickness(18, 12, 18, 12)
                };

                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftStack = new StackPanel { Orientation = Orientation.Horizontal };
                Grid.SetColumn(leftStack, 0);

                var qNum = new TextBlock
                {
                    Text = $"Question {q.Order}",
                    Style = (Style)FindResource("T.QuestionNumber"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new System.Windows.Thickness(0, 0, 12, 0)
                };
                leftStack.Children.Add(qNum);

                var typeBadge = new Border
                {
                    Background = GetTypeBadgeBackground(q.Type),
                    BorderBrush = GetTypeBadgeBorder(q.Type),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new System.Windows.Thickness(7, 2, 7, 2)
                };
                var typeBadgeText = new TextBlock
                {
                    Text = q.Type,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetTypeBadgeForeground(q.Type)
                };
                typeBadge.Child = typeBadgeText;
                leftStack.Children.Add(typeBadge);

                var rightStack = new StackPanel { Orientation = Orientation.Horizontal };
                Grid.SetColumn(rightStack, 1);

                var pointsText = new TextBlock
                {
                    Text = "Points: ",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("Text.Secondary"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                rightStack.Children.Add(pointsText);

                var pointsBox = new TextBox
                {
                    Text = q.Points.ToString(),
                    Style = (Style)FindResource("Input.Points"),
                    Width = 60,
                    Tag = q
                };
                pointsBox.TextChanged += (s, e) =>
                {
                    if (int.TryParse(pointsBox.Text, out int val))
                    {
                        (pointsBox.Tag as QuestionItem).Points = val;
                        UpdateActiveSectionHeader();
                    }
                };
                rightStack.Children.Add(pointsBox);

                var deleteBtn = new Button
                {
                    Style = (Style)FindResource("Btn.Ghost"),
                    Padding = new System.Windows.Thickness(6, 4, 6, 4),
                    Margin = new System.Windows.Thickness(8, 0, 0, 0),
                    Tag = q.Id,
                    Content = new TextBlock
                    {
                        Text = "✕",
                        FontSize = 16,
                        Foreground = (Brush)FindResource("Text.Muted")
                    }
                };
                deleteBtn.Click += DeleteQuestion_Click;
                rightStack.Children.Add(deleteBtn);

                headerGrid.Children.Add(leftStack);
                headerGrid.Children.Add(rightStack);
                headerBorder.Child = headerGrid;
                mainStack.Children.Add(headerBorder);

                // ---- Body ----
                var bodyStack = new StackPanel { Margin = new System.Windows.Thickness(18, 16, 18, 16) };

                var promptContainer = new Grid
                {
                    Margin = new System.Windows.Thickness(0, 0, 0, 4)
                };
                promptContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                promptContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                double currentHeight = q.PromptHeight >= 80 ? q.PromptHeight : 80;
                var questionTextBox = new TextBox
                {
                    Text = q.Text,
                    Style = (Style)FindResource("Input.Text"),
                    MinHeight = 80,
                    MaxHeight = 500,
                    Height = currentHeight,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Tag = q
                };
                questionTextBox.TextChanged += (s, e) =>
                {
                    (questionTextBox.Tag as QuestionItem).Text = questionTextBox.Text;
                };
                Grid.SetRow(questionTextBox, 0);
                promptContainer.Children.Add(questionTextBox);

                var resizeGrip = new System.Windows.Controls.Primitives.Thumb
                {
                    Style = (Style)FindResource("Thumb.PromptResizeGrip"),
                    ToolTip = "Drag to resize prompt height"
                };
                resizeGrip.DragDelta += (s, e) =>
                {
                    double newHeight = Math.Clamp(questionTextBox.ActualHeight + e.VerticalChange, 80, 500);
                    questionTextBox.Height = newHeight;
                    q.PromptHeight = newHeight;
                };
                Grid.SetRow(resizeGrip, 1);
                promptContainer.Children.Add(resizeGrip);

                bodyStack.Children.Add(promptContainer);

                var properType = GetProperTypeName(q.Type);

                switch (properType)
                {
                    case "Identification":
                        var idLabel = new TextBlock
                        {
                            Text = "Correct Answer",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 8, 0, 6)
                        };
                        bodyStack.Children.Add(idLabel);

                        var idAnswer = new TextBox
                        {
                            Text = q.ExpectedAnswer,
                            Style = (Style)FindResource("Input.AnswerOption"),
                            AcceptsReturn = false,
                            Tag = q
                        };
                        idAnswer.TextChanged += (s, e) =>
                        {
                            (idAnswer.Tag as QuestionItem).ExpectedAnswer = idAnswer.Text;
                        };
                        bodyStack.Children.Add(idAnswer);
                        break;

                    case "Multiple Choice":
                        var mcLabel = new TextBlock
                        {
                            Text = "Answer Options (click letter to mark correct)",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 8, 0, 6)
                        };
                        bodyStack.Children.Add(mcLabel);

                        bodyStack.Children.Add(CreateEditableOptionRow("A", q));
                        bodyStack.Children.Add(CreateEditableOptionRow("B", q));
                        bodyStack.Children.Add(CreateEditableOptionRow("C", q));
                        bodyStack.Children.Add(CreateEditableOptionRow("D", q));
                        break;

                    case "True or False":
                        var tfLabel = new TextBlock
                        {
                            Text = "Correct Answer (click to toggle)",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 8, 0, 6)
                        };
                        bodyStack.Children.Add(tfLabel);

                        var tfPanel = new StackPanel { Orientation = Orientation.Horizontal };

                        var trueBorder = new Border
                        {
                            Background = q.IsTrue == true ? (Brush)FindResource("Status.SuccessBg") : (Brush)FindResource("Bg.Base"),
                            BorderBrush = q.IsTrue == true ? (Brush)FindResource("Status.Success") : (Brush)FindResource("Border.Medium"),
                            BorderThickness = new System.Windows.Thickness(q.IsTrue == true ? 2 : 1),
                            CornerRadius = new CornerRadius(6),
                            Padding = q.IsTrue == true ? new System.Windows.Thickness(18, 10, 18, 10) : new System.Windows.Thickness(28, 10, 18, 10),
                            Margin = new System.Windows.Thickness(0, 0, 10, 0),
                            Cursor = Cursors.Hand,
                            Tag = q
                        };
                        var trueText = new TextBlock
                        {
                            Text = q.IsTrue == true ? "✓ True" : "True",
                            FontSize = 14,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = q.IsTrue == true ? (Brush)FindResource("Status.Success") : (Brush)FindResource("Text.Secondary")
                        };
                        trueBorder.Child = trueText;
                        trueBorder.MouseLeftButtonDown += (s, e) =>
                        {
                            var qItem = (trueBorder.Tag as QuestionItem);
                            qItem.IsTrue = true;
                            UpdateQuestionsDisplay();
                        };
                        tfPanel.Children.Add(trueBorder);

                        var falseBorder = new Border
                        {
                            Background = q.IsTrue == false ? (Brush)FindResource("Status.SuccessBg") : (Brush)FindResource("Bg.Base"),
                            BorderBrush = q.IsTrue == false ? (Brush)FindResource("Status.Success") : (Brush)FindResource("Border.Medium"),
                            BorderThickness = new System.Windows.Thickness(q.IsTrue == false ? 2 : 1),
                            CornerRadius = new CornerRadius(6),
                            Padding = q.IsTrue == false ? new System.Windows.Thickness(18, 10, 18, 10) : new System.Windows.Thickness(28, 10, 18, 10),
                            Cursor = Cursors.Hand,
                            Tag = q
                        };
                        var falseText = new TextBlock
                        {
                            Text = q.IsTrue == false ? "✓ False" : "False",
                            FontSize = 14,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = q.IsTrue == false ? (Brush)FindResource("Status.Success") : (Brush)FindResource("Text.Secondary")
                        };
                        falseBorder.Child = falseText;
                        falseBorder.MouseLeftButtonDown += (s, e) =>
                        {
                            var qItem = (falseBorder.Tag as QuestionItem);
                            qItem.IsTrue = false;
                            UpdateQuestionsDisplay();
                        };
                        tfPanel.Children.Add(falseBorder);

                        bodyStack.Children.Add(tfPanel);
                        break;

                    case "Essay":
                        var wordLimitPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new System.Windows.Thickness(0, 12, 0, 0) };
                        wordLimitPanel.Children.Add(new TextBlock
                        {
                            Text = "Word Limit: 150 to 500 words",
                            FontSize = 12,
                            Foreground = (Brush)FindResource("Text.Secondary"),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        bodyStack.Children.Add(wordLimitPanel);

                        var refLabelEssay = new TextBlock
                        {
                            Text = "Expected Key Points / Reference Answer (Optional context for AI grading)",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 10, 0, 4)
                        };
                        bodyStack.Children.Add(refLabelEssay);

                        var refBoxEssay = new TextBox
                        {
                            Style = (Style)FindResource("Input.Text"),
                            MinHeight = 60,
                            AcceptsReturn = true,
                            TextWrapping = TextWrapping.Wrap,
                            Text = q.CorrectAnswer ?? string.Empty,
                            Tag = q
                        };
                        refBoxEssay.TextChanged += (s, e) =>
                        {
                            if (refBoxEssay.Tag is QuestionItem qi)
                            {
                                qi.CorrectAnswer = refBoxEssay.Text;
                            }
                        };
                        bodyStack.Children.Add(refBoxEssay);

                        bodyStack.Children.Add(CreateAIRubricSection(q));
                        break;

                    case "Enumeration":
                        var enumLabel = new TextBlock
                        {
                            Text = "Accepted Answers (one per line)",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 8, 0, 6)
                        };
                        bodyStack.Children.Add(enumLabel);

                        var enumItemsPanel = new StackPanel();
                        var answers = q.ExpectedAnswers?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(a => a.Trim()).ToList() ?? new List<string>();
                        if (answers.Count == 0) answers = new List<string> { "Item 1", "Item 2", "Item 3", "Item 4" };

                        q.ExpectedAnswers = string.Join(",", answers);

                        RebuildEnumerationItems(enumItemsPanel, q);

                        bodyStack.Children.Add(enumItemsPanel);

                        var addItemBtn = new Button
                        {
                            Style = (Style)FindResource("Btn.Ghost"),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Tag = q,
                            Content = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Children = {
                                    new TextBlock { Text = "➕", FontSize = 12, Foreground = (Brush)FindResource("Accent.Primary"), Margin = new System.Windows.Thickness(0,0,5,0), VerticalAlignment = VerticalAlignment.Center },
                                    new TextBlock { Text = "Add answer item", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Accent.Primary"), VerticalAlignment = VerticalAlignment.Center }
                                }
                            }
                        };
                        addItemBtn.Click += (s, e) =>
                        {
                            var qItem = (addItemBtn.Tag as QuestionItem);
                            var list = qItem.ExpectedAnswers?.Split(',').ToList() ?? new List<string>();
                            list.Add($"Item {list.Count + 1}");
                            qItem.ExpectedAnswers = string.Join(",", list);
                            UpdateQuestionsDisplay();
                        };
                        bodyStack.Children.Add(addItemBtn);
                        break;

                    case "Coding":
                        var langLabel = new TextBlock
                        {
                            Text = "Target Language",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 8, 0, 6)
                        };
                        bodyStack.Children.Add(langLabel);

                        var langPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new System.Windows.Thickness(0, 0, 0, 16)
                        };

                        string activeLang = (!string.IsNullOrEmpty(q.ProgrammingLanguage) && _supportedLanguages.Contains(q.ProgrammingLanguage))
                            ? q.ProgrammingLanguage
                            : "C";
                        q.ProgrammingLanguage = activeLang;

                        foreach (var lang in _supportedLanguages)
                        {
                            bool isSelected = activeLang == lang;
                            var border = new Border
                            {
                                Background = isSelected ? (Brush)FindResource("Accent.PrimaryBg") : (Brush)FindResource("Bg.Base"),
                                BorderBrush = isSelected ? (Brush)FindResource("Accent.Primary") : (Brush)FindResource("Border.Medium"),
                                BorderThickness = new System.Windows.Thickness(1),
                                CornerRadius = new CornerRadius(6),
                                Padding = new System.Windows.Thickness(14, 7, 14, 7),
                                Margin = new System.Windows.Thickness(0, 0, 8, 0),
                                Cursor = Cursors.Hand,
                                Tag = new { Question = q, Language = lang }
                            };

                            var innerStack = new StackPanel { Orientation = Orientation.Horizontal };
                            var dotGrid = new Grid { Width = 16, Height = 16, Margin = new System.Windows.Thickness(0, 0, 6, 0) };
                            dotGrid.Children.Add(new Ellipse
                            {
                                Width = 16,
                                Height = 16,
                                Stroke = isSelected ? (Brush)FindResource("Accent.Primary") : (Brush)FindResource("Border.Medium"),
                                Fill = System.Windows.Media.Brushes.Transparent
                            });
                            if (isSelected)
                            {
                                dotGrid.Children.Add(new Ellipse
                                {
                                    Width = 8,
                                    Height = 8,
                                    Fill = (Brush)FindResource("Accent.Primary"),
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center
                                });
                            }
                            innerStack.Children.Add(dotGrid);

                            var langText = new TextBlock
                            {
                                Text = lang,
                                FontSize = 13,
                                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                                Foreground = isSelected ? (Brush)FindResource("Accent.Primary") : (Brush)FindResource("Text.Secondary"),
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            innerStack.Children.Add(langText);
                            border.Child = innerStack;

                            border.MouseLeftButtonDown += (s, e) =>
                            {
                                var data = (dynamic)((Border)s).Tag;
                                var qItem = data.Question as QuestionItem;
                                qItem.ProgrammingLanguage = data.Language;

                                // Update only the language buttons in this langPanel visually
                                foreach (var child in langPanel.Children)
                                {
                                    if (child is not Border lb) continue;
                                    var ldata = (dynamic)lb.Tag;
                                    bool sel = ldata.Language == data.Language;
                                    lb.Background = sel ? (Brush)FindResource("Accent.PrimaryBg") : (Brush)FindResource("Bg.Base");
                                    lb.BorderBrush = sel ? (Brush)FindResource("Accent.Primary") : (Brush)FindResource("Border.Medium");

                                    // Update the dot and text inside
                                    if (lb.Child is StackPanel sp && sp.Children.Count >= 2)
                                    {
                                        if (sp.Children[0] is Grid dotGrid)
                                        {
                                            // Remove inner filled dot if exists
                                            if (dotGrid.Children.Count > 1)
                                                dotGrid.Children.RemoveAt(1);

                                            if (sel)
                                            {
                                                dotGrid.Children.Add(new Ellipse
                                                {
                                                    Width = 8,
                                                    Height = 8,
                                                    Fill = (Brush)FindResource("Accent.Primary"),
                                                    HorizontalAlignment = HorizontalAlignment.Center,
                                                    VerticalAlignment = VerticalAlignment.Center
                                                });
                                            }

                                            // Update outer ellipse stroke
                                            if (dotGrid.Children[0] is Ellipse outerDot)
                                            {
                                                outerDot.Stroke = sel
                                                    ? (Brush)FindResource("Accent.Primary")
                                                    : (Brush)FindResource("Border.Medium");
                                            }
                                        }

                                        if (sp.Children[1] is TextBlock langText)
                                        {
                                            langText.Foreground = sel
                                                ? (Brush)FindResource("Accent.Primary")
                                                : (Brush)FindResource("Text.Secondary");
                                            langText.FontWeight = sel ? FontWeights.SemiBold : FontWeights.Normal;
                                        }
                                    }
                                }
                            };

                            langPanel.Children.Add(border);
                        }

                        bodyStack.Children.Add(langPanel);

                        var stubLabel = new TextBlock
                        {
                            Text = "Starter Code Stub (pre-populates student IDE)",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 8, 0, 6)
                        };
                        bodyStack.Children.Add(stubLabel);

                        var stubBox = new TextBox
                        {
                            Text = q.StarterCode ?? "// Write your code here",
                            Style = (Style)FindResource("Input.Text"),
                            Height = 60,
                            FontFamily = new FontFamily("Consolas"),
                            AcceptsReturn = true,
                            IsReadOnly = false,
                            Tag = q
                        };
                        stubBox.TextChanged += (s, e) =>
                        {
                            if (stubBox.Tag is QuestionItem qi)
                                qi.StarterCode = stubBox.Text;
                        };
                        bodyStack.Children.Add(stubBox);

                        if (q.TestCases != null && q.TestCases.Count > 0)
                        {
                            var tcHeader = new TextBlock
                            {
                                Text = $"Test Cases ({q.TestCases.Count})",
                                Style = (Style)FindResource("T.FieldLabel"),
                                Margin = new System.Windows.Thickness(0, 8, 0, 4)
                            };
                            bodyStack.Children.Add(tcHeader);

                            var tcPanel = new StackPanel { Margin = new System.Windows.Thickness(0, 0, 0, 6) };
                            foreach (var tc in q.TestCases)
                            {
                                var tcText = new TextBlock
                                {
                                    Text = $"• In: {tc.Input}  →  Out: {tc.ExpectedOutput}",
                                    FontFamily = new FontFamily("Consolas"),
                                    FontSize = 11,
                                    Foreground = (Brush)FindResource("Text.Secondary"),
                                    Margin = new System.Windows.Thickness(8, 2, 0, 2)
                                };
                                tcPanel.Children.Add(tcText);
                            }
                            bodyStack.Children.Add(tcPanel);
                        }

                        var refLabelCoding = new TextBlock
                        {
                            Text = "Expected Key Points / Reference Solution (Optional context for AI grading)",
                            Style = (Style)FindResource("T.FieldLabel"),
                            Margin = new System.Windows.Thickness(0, 10, 0, 4)
                        };
                        bodyStack.Children.Add(refLabelCoding);

                        var refBoxCoding = new TextBox
                        {
                            Style = (Style)FindResource("Input.Text"),
                            FontFamily = new FontFamily("Consolas"),
                            MinHeight = 65,
                            AcceptsReturn = true,
                            TextWrapping = TextWrapping.Wrap,
                            Text = q.CorrectAnswer ?? string.Empty,
                            Tag = q
                        };
                        refBoxCoding.TextChanged += (s, e) =>
                        {
                            if (refBoxCoding.Tag is QuestionItem qi)
                            {
                                qi.CorrectAnswer = refBoxCoding.Text;
                            }
                        };
                        bodyStack.Children.Add(refBoxCoding);

                        bodyStack.Children.Add(CreateAIRubricSection(q));
                        break;
                }

                accentBorder.Child = bodyStack;
                mainStack.Children.Add(accentBorder);

                cardBorder.Child = mainStack;
                container.Children.Add(cardBorder);
            }
        }

        private void RebuildEnumerationItems(StackPanel panel, QuestionItem q)
        {
            panel.Children.Clear();
            var answers = q.ExpectedAnswers?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim()).ToList() ?? new List<string>();

            for (int i = 0; i < answers.Count; i++)
            {
                int idx = i;
                var grid = new Grid { Margin = new System.Windows.Thickness(0, 0, 0, 8) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var numBorder = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = (Brush)FindResource("Accent.PrimaryBg"),
                    BorderBrush = (Brush)FindResource("Accent.Primary"),
                    BorderThickness = new System.Windows.Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center
                };
                numBorder.Child = new TextBlock
                {
                    Text = (idx + 1).ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("Accent.Primary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(numBorder, 0);
                grid.Children.Add(numBorder);

                var answerBox = new TextBox
                {
                    Text = answers[idx],
                    Style = (Style)FindResource("Input.AnswerOption"),
                    AcceptsReturn = false,
                    Tag = new { Question = q, Index = idx }
                };
                answerBox.TextChanged += (s, e) =>
                {
                    var data = (dynamic)((TextBox)s).Tag;
                    var qItem = data.Question as QuestionItem;
                    int index = data.Index;
                    var list = qItem.ExpectedAnswers?.Split(',').ToList() ?? new List<string>();
                    if (index < list.Count)
                    {
                        list[index] = ((TextBox)s).Text;
                        qItem.ExpectedAnswers = string.Join(",", list);
                    }
                };
                Grid.SetColumn(answerBox, 1);
                grid.Children.Add(answerBox);

                if (answers.Count > 1)
                {
                    var removeBtn = new Button
                    {
                        Style = (Style)FindResource("Btn.Ghost"),
                        Padding = new System.Windows.Thickness(4, 0, 4, 0),
                        Margin = new System.Windows.Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = new { Question = q, Index = idx },
                        Content = new TextBlock
                        {
                            Text = "✕",
                            FontSize = 14,
                            Foreground = (Brush)FindResource("Text.Muted")
                        }
                    };
                    removeBtn.Click += (s, e) =>
                    {
                        var data = (dynamic)((Button)s).Tag;
                        var qItem = data.Question as QuestionItem;
                        int index = data.Index;
                        var list = qItem.ExpectedAnswers?.Split(',').ToList() ?? new List<string>();
                        if (list.Count > 1 && index < list.Count)
                        {
                            list.RemoveAt(index);
                            qItem.ExpectedAnswers = string.Join(",", list);
                            UpdateQuestionsDisplay();
                        }
                    };
                    Grid.SetColumn(removeBtn, 2);
                    grid.Children.Add(removeBtn);
                }

                panel.Children.Add(grid);
            }
        }

        private StackPanel CreateEditableOptionRow(string letter, QuestionItem q)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new System.Windows.Thickness(0, 0, 0, 8) };

            string optionText = GetOptionText(q, letter);
            bool isCorrect = string.Equals(q.CorrectAnswer?.Trim(), letter, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(optionText) && string.Equals(q.CorrectAnswer?.Trim(), optionText.Trim(), StringComparison.OrdinalIgnoreCase));

            var toggleBtn = new Button
            {
                Style = isCorrect ? (Style)FindResource("Btn.AnswerToggle.Correct") : (Style)FindResource("Btn.AnswerToggle"),
                Width = 30,
                Tag = new { Question = q, Letter = letter }
            };
            toggleBtn.Content = new TextBlock { Text = letter, FontSize = 12, FontWeight = FontWeights.Bold };
            toggleBtn.Click += (s, e) =>
            {
                var data = (dynamic)((Button)s).Tag;
                var qItem = data.Question as QuestionItem;
                qItem.CorrectAnswer = data.Letter;
                UpdateQuestionsDisplay();
            };
            row.Children.Add(toggleBtn);

            var optionBox = new TextBox
            {
                Text = optionText,
                Style = (Style)FindResource("Input.AnswerOption"),
                AcceptsReturn = false,
                Tag = new { Question = q, Letter = letter }
            };
            optionBox.TextChanged += (s, e) =>
            {
                var data = (dynamic)((TextBox)s).Tag;
                var qItem = data.Question as QuestionItem;
                string l = data.Letter;
                SetOptionText(qItem, l, optionBox.Text);
            };
            row.Children.Add(optionBox);

            if (isCorrect)
            {
                var badge = new Border
                {
                    Background = (Brush)FindResource("Status.SuccessBg"),
                    BorderBrush = (Brush)FindResource("Status.Success"),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new System.Windows.Thickness(8, 4, 8, 4),
                    Margin = new System.Windows.Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = "✓ Correct",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("Status.Success")
                };
                row.Children.Add(badge);
            }
            else
            {
                var spacer = new Border { Width = 70, Margin = new System.Windows.Thickness(8, 0, 0, 0) };
                row.Children.Add(spacer);
            }

            return row;
        }

        private string GetOptionText(QuestionItem q, string letter)
        {
            switch (letter)
            {
                case "A": return q.OptionA;
                case "B": return q.OptionB;
                case "C": return q.OptionC;
                case "D": return q.OptionD;
                default: return "";
            }
        }

        private void SetOptionText(QuestionItem q, string letter, string value)
        {
            switch (letter)
            {
                case "A": q.OptionA = value; break;
                case "B": q.OptionB = value; break;
                case "C": q.OptionC = value; break;
                case "D": q.OptionD = value; break;
            }
        }

        private void AddQuestionByType_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var questionType = button.Tag?.ToString();
            if (string.IsNullOrEmpty(questionType)) return;

            if (_selectedSection == null)
            {
                ToastNotification.Show("Please select a section first.", Application.Current.MainWindow, 3, true);
                return;
            }

            ResetAllTypeButtons();
            button.Background = (Brush)FindResource("Accent.PrimaryBg");
            button.BorderBrush = (Brush)FindResource("Accent.Primary");
            button.BorderThickness = new System.Windows.Thickness(2);
            FillTypeDot(button, true);
            _selectedQuestionType = questionType;

            var newQuestion = CreateQuestionByType(questionType);
            newQuestion.Order = _selectedSection.Questions.Count + 1;
            newQuestion.SectionId = _selectedSection.Id;
            _selectedSection.Questions.Add(newQuestion);

            UpdateQuestionsDisplay();
            UpdateSectionList();
            UpdateActiveSectionHeader();

            ToastNotification.Show($"New {questionType} question added!", Application.Current.MainWindow);
        }

        private QuestionItem CreateQuestionByType(string questionType)
        {
            var properType = GetProperTypeName(questionType);

            var newQuestion = new QuestionItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = properType,
                Points = 1,
                Text = $"New {properType} Question - Edit me!"
            };

            switch (properType)
            {
                case "Identification":
                    newQuestion.ExpectedAnswer = "Enter correct answer...";
                    newQuestion.Text = "Enter your identification question here...";
                    break;
                case "Multiple Choice":
                    newQuestion.OptionA = "Option A";
                    newQuestion.OptionB = "Option B";
                    newQuestion.OptionC = "Option C";
                    newQuestion.OptionD = "Option D";
                    newQuestion.CorrectAnswer = "A";
                    newQuestion.Text = "Enter your multiple choice question here...";
                    break;
                case "True or False":
                    newQuestion.IsTrue = true;
                    newQuestion.Text = "Enter your true/false question here...";
                    break;
                case "Essay":
                    newQuestion.Text = "Enter your essay question here...";
                    break;
                case "Enumeration":
                    newQuestion.ExpectedAnswers = "Item 1, Item 2, Item 3, Item 4";
                    newQuestion.Text = "Enter your enumeration question here...";
                    break;
                case "Coding":
                    newQuestion.ProgrammingLanguage = "C";
                    newQuestion.Text = "Enter your coding problem description here...";
                    break;
                default:
                    newQuestion.Text = "Enter your question here...";
                    break;
            }

            return newQuestion;
        }

        private void AddNewQuestion_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSection == null)
            {
                ToastNotification.Show("Please select a section first.", Application.Current.MainWindow, 3, true);
                return;
            }

            var questionType = string.IsNullOrEmpty(_selectedQuestionType)
                ? _selectedSection.Type
                : _selectedQuestionType;

            var newQuestion = CreateQuestionByType(questionType);
            newQuestion.Order = _selectedSection.Questions.Count + 1;
            newQuestion.SectionId = _selectedSection.Id;
            _selectedSection.Questions.Add(newQuestion);

            UpdateQuestionsDisplay();
            UpdateSectionList();
            UpdateActiveSectionHeader();

            ToastNotification.Show($"New {questionType} question added!", Application.Current.MainWindow);
        }

        private void DeleteQuestion_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var questionId = button.Tag?.ToString();
            if (string.IsNullOrEmpty(questionId)) return;

            if (_selectedSection != null)
            {
                var question = _selectedSection.Questions.Find(q => q.Id == questionId);
                if (question != null)
                {
                    var result = MessageBox.Show($"Delete question '{question.Text.Substring(0, Math.Min(30, question.Text.Length))}...'?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _selectedSection.Questions.Remove(question);
                        for (int i = 0; i < _selectedSection.Questions.Count; i++)
                            _selectedSection.Questions[i].Order = i + 1;

                        UpdateQuestionsDisplay();
                        UpdateSectionList();
                        UpdateActiveSectionHeader();
                    }
                }
            }
        }

        private void DeleteSectionBtn_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is not Button btn || btn.Tag is not ExamSection section)
                return;

            _sectionPendingDelete = section;

            if (DeleteSectionPromptText != null)
            {
                int qCount = section.Questions.Count;
                string qPhrase = qCount == 1 ? "1 question" : $"{qCount} questions";
                DeleteSectionPromptText.Text = $"Delete '{section.Name}' and its {qPhrase}? This action cannot be undone.";
            }

            if (DeleteSectionModalOverlay != null)
            {
                DeleteSectionModalOverlay.Visibility = Visibility.Visible;
            }
        }

        private void CancelDeleteSection_Click(object sender, RoutedEventArgs e)
        {
            _sectionPendingDelete = null;
            if (DeleteSectionModalOverlay != null)
            {
                DeleteSectionModalOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void DeleteSectionOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == DeleteSectionModalOverlay)
            {
                CancelDeleteSection_Click(sender, e);
            }
        }

        private void ConfirmDeleteSection_Click(object sender, RoutedEventArgs e)
        {
            if (_sectionPendingDelete == null) return;

            var targetSection = _sectionPendingDelete;
            string sectionName = targetSection.Name;

            foreach (var q in targetSection.Questions)
            {
                _rubricData.Remove(q.Id);
            }

            int deletedIndex = _sections.IndexOf(targetSection);
            bool wasActive = targetSection == _selectedSection || targetSection.IsActive;

            _sections.Remove(targetSection);

            if (wasActive)
            {
                if (_sections.Count > 0)
                {
                    int newIndex = Math.Min(deletedIndex, _sections.Count - 1);
                    var nextSection = _sections[newIndex];

                    foreach (var s in _sections)
                        s.IsActive = false;

                    nextSection.IsActive = true;
                    _selectedSection = nextSection;
                    SelectTypeButton(nextSection.Type);
                }
                else
                {
                    _selectedSection = null;
                    ResetAllTypeButtons();
                }
            }

            _sectionPendingDelete = null;
            if (DeleteSectionModalOverlay != null)
            {
                DeleteSectionModalOverlay.Visibility = Visibility.Collapsed;
            }

            UpdateSectionList();
            UpdateQuestionsDisplay();
            UpdateActiveSectionHeader();

            ToastNotification.Show($"Section '{sectionName}' deleted.", Application.Current.MainWindow);
        }

        private Brush GetTypeBadgeBackground(string type)
        {
            if (string.IsNullOrEmpty(type)) return (Brush)FindResource("Bg.Base");

            var lowerType = type.ToLower();
            return lowerType switch
            {
                "coding" => (Brush)FindResource("Status.SuccessBg"),
                "identification" => (Brush)FindResource("Bg.Base"),
                "multiple choice" => (Brush)FindResource("Accent.PrimaryBg"),
                "true or false" => (Brush)FindResource("Status.SuccessBg"),
                "essay" => (Brush)FindResource("AI.Bg"),
                "enumeration" => (Brush)FindResource("Status.WarningBg"),
                _ => (Brush)FindResource("Bg.Base")
            };
        }

        private Brush GetTypeBadgeBorder(string type)
        {
            if (string.IsNullOrEmpty(type)) return (Brush)FindResource("Border.Medium");

            var lowerType = type.ToLower();
            return lowerType switch
            {
                "coding" => (Brush)FindResource("Status.Success"),
                "identification" => (Brush)FindResource("Border.Medium"),
                "multiple choice" => (Brush)FindResource("Accent.Primary"),
                "true or false" => (Brush)FindResource("Status.Success"),
                "essay" => (Brush)FindResource("AI.Border"),
                "enumeration" => (Brush)FindResource("Status.Warning"),
                _ => (Brush)FindResource("Border.Medium")
            };
        }

        private Brush GetTypeBadgeForeground(string type)
        {
            if (string.IsNullOrEmpty(type)) return (Brush)FindResource("Text.Secondary");

            var lowerType = type.ToLower();
            return lowerType switch
            {
                "coding" => (Brush)FindResource("Status.Success"),
                "identification" => (Brush)FindResource("Text.Secondary"),
                "multiple choice" => (Brush)FindResource("Accent.Primary"),
                "true or false" => (Brush)FindResource("Status.Success"),
                "essay" => (Brush)FindResource("AI.Text"),
                "enumeration" => (Brush)FindResource("Status.Warning"),
                _ => (Brush)FindResource("Text.Secondary")
            };
        }

        private Brush GetTypeColor(string type)
        {
            if (string.IsNullOrEmpty(type)) return (Brush)FindResource("QType.Identification");

            var lowerType = type.ToLower();
            if (lowerType.Contains("identification"))
                return (Brush)FindResource("QType.Identification");
            if (lowerType.Contains("multiple") || lowerType.Contains("choice"))
                return (Brush)FindResource("QType.MultipleChoice");
            if (lowerType.Contains("true") || lowerType.Contains("false"))
                return (Brush)FindResource("QType.TrueOrFalse");
            if (lowerType.Contains("essay"))
                return (Brush)FindResource("QType.Essay");
            if (lowerType.Contains("enumeration"))
                return (Brush)FindResource("QType.Enumeration");
            if (lowerType.Contains("coding") || lowerType.Contains("code"))
                return (Brush)FindResource("QType.Coding");

            return (Brush)FindResource("QType.Identification");
        }

        private Border CreateAIRubricSection(QuestionItem question)
        {
            var border = new Border
            {
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new System.Windows.Thickness(14, 12, 14, 12),
                Margin = new System.Windows.Thickness(0, 16, 0, 0)
            };

            PopulateAIRubricContent(border, question);
            return border;
        }

        private void PopulateAIRubricContent(Border border, QuestionItem question)
        {
            border.Child = null;

            bool isAttached = _rubricData.TryGetValue(question.Id, out var existingRubric) && existingRubric != null;

            if (!isAttached)
            {
                border.Background = (Brush)FindResource("AI.Bg");
                border.BorderBrush = (Brush)FindResource("AI.Border");

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(leftStack, 0);
                var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new System.Windows.Thickness(0, 0, 0, 3) };
                titleStack.Children.Add(new TextBlock
                {
                    Text = "🤖",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("AI.Border"),
                    Margin = new System.Windows.Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                titleStack.Children.Add(new TextBlock
                {
                    Text = "AI Auto-Grading Configuration",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("AI.Text")
                });
                leftStack.Children.Add(titleStack);
                leftStack.Children.Add(new TextBlock
                {
                    Text = "Set rubric criteria the AI uses to evaluate and score this question.",
                    FontSize = 11,
                    Foreground = (Brush)FindResource("AI.Text"),
                    TextWrapping = TextWrapping.Wrap
                });

                var btn = new Button
                {
                    Style = (Style)FindResource("Btn.AIRubric"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = question
                };
                var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
                btnStack.Children.Add(new TextBlock
                {
                    Text = "➕",
                    FontSize = 11,
                    Foreground = (Brush)FindResource("AI.Border"),
                    Margin = new System.Windows.Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                btnStack.Children.Add(new TextBlock
                {
                    Text = "Configure AI Rubric",
                    VerticalAlignment = VerticalAlignment.Center
                });
                btn.Content = btnStack;

                btn.Click += (s, e) =>
                {
                    var rubricModal = new AIRubricModal { Owner = Application.Current.MainWindow };
                    rubricModal.QuestionTitle = question.Text ?? "Question";
                    rubricModal.QuestionId = question.Id;

                    if (rubricModal.ShowDialog() == true)
                    {
                        var rubricData = rubricModal.GetRubricData();
                        rubricData.AttachmentFilePath = rubricModal.FilePath;
                        rubricData.Scope = rubricModal.RubricScope;

                        if (string.Equals(rubricData.Scope, "Section", StringComparison.OrdinalIgnoreCase))
                        {
                            var targetSection = _sections.FirstOrDefault(sec => sec.Questions.Any(qu => qu.Id == question.Id));
                            if (targetSection != null)
                            {
                                foreach (var sq in targetSection.Questions.Where(qu => qu.Type == "Essay" || qu.Type == "Coding"))
                                {
                                    _rubricData[sq.Id] = rubricData;
                                }
                            }
                            UpdateQuestionsDisplay();
                        }
                        else
                        {
                            _rubricData[question.Id] = rubricData;
                            PopulateAIRubricContent(border, question);
                        }

                        ToastNotification.Show("Rubric saved successfully!", Application.Current.MainWindow);
                    }
                };
                Grid.SetColumn(btn, 1);
                grid.Children.Add(leftStack);
                grid.Children.Add(btn);

                border.Child = grid;
            }
            else
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F3FF"));
                border.BorderBrush = (Brush)FindResource("AI.Border");

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(leftStack, 0);

                var badgePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new System.Windows.Thickness(0, 0, 0, 4) };

                string rubricTitle = string.IsNullOrWhiteSpace(existingRubric!.Description) ? "Rubric Configuration" : existingRubric.Description;
                int totalPts = existingRubric.TotalWeight > 0 ? existingRubric.TotalWeight : question.Points;

                var titlePill = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDE9FE")),
                    BorderBrush = (Brush)FindResource("AI.Border"),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new System.Windows.Thickness(10, 3, 10, 3),
                    Margin = new System.Windows.Thickness(0, 0, 8, 0)
                };
                var pillText = new TextBlock
                {
                    Text = $"📋 Rubric: {rubricTitle} ({totalPts} pts)",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("AI.Text"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                titlePill.Child = pillText;
                badgePanel.Children.Add(titlePill);

                bool isSectionScope = string.Equals(existingRubric.Scope, "Section", StringComparison.OrdinalIgnoreCase);
                var scopePill = new Border
                {
                    Background = isSectionScope ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                    BorderBrush = isSectionScope ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new System.Windows.Thickness(8, 3, 8, 3)
                };
                var scopeText = new TextBlock
                {
                    Text = isSectionScope ? "[Entire Section]" : "[Question Only]",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isSectionScope ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D4ED8")) : (Brush)FindResource("Text.Secondary"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                scopePill.Child = scopeText;
                badgePanel.Children.Add(scopePill);

                leftStack.Children.Add(badgePanel);

                int critCount = existingRubric.Criteria?.Count ?? 0;
                string attachInfo = !string.IsNullOrEmpty(existingRubric.AttachmentFilePath) ? " · 📎 Reference file attached" : "";
                var descBlock = new TextBlock
                {
                    Text = $"Active AI auto-grading · {critCount} criteria configured{attachInfo}",
                    FontSize = 11,
                    Foreground = (Brush)FindResource("AI.Text")
                };
                leftStack.Children.Add(descBlock);

                var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(rightStack, 1);

                var editBtn = new Button
                {
                    Style = (Style)FindResource("Btn.Outlined"),
                    Margin = new System.Windows.Thickness(0, 0, 6, 0),
                    Padding = new System.Windows.Thickness(10, 6, 10, 6),
                    Cursor = Cursors.Hand
                };
                var editStack = new StackPanel { Orientation = Orientation.Horizontal };
                editStack.Children.Add(new TextBlock
                {
                    Text = "✎",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("Accent.Primary"),
                    Margin = new System.Windows.Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                editStack.Children.Add(new TextBlock
                {
                    Text = "Edit Rubric",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Accent.Primary"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                editBtn.Content = editStack;

                editBtn.Click += (s, e) =>
                {
                    var rubricModal = new AIRubricModal { Owner = Application.Current.MainWindow };
                    rubricModal.QuestionTitle = question.Text ?? "Question";
                    rubricModal.QuestionId = question.Id;
                    rubricModal.LoadExistingRubric(existingRubric, existingRubric.Scope);

                    if (rubricModal.ShowDialog() == true)
                    {
                        var updated = rubricModal.GetRubricData();
                        updated.AttachmentFilePath = rubricModal.FilePath;
                        updated.Scope = rubricModal.RubricScope;

                        if (string.Equals(updated.Scope, "Section", StringComparison.OrdinalIgnoreCase))
                        {
                            var targetSection = _sections.FirstOrDefault(sec => sec.Questions.Any(qu => qu.Id == question.Id));
                            if (targetSection != null)
                            {
                                foreach (var sq in targetSection.Questions.Where(qu => qu.Type == "Essay" || qu.Type == "Coding"))
                                {
                                    _rubricData[sq.Id] = updated;
                                }
                            }
                            UpdateQuestionsDisplay();
                        }
                        else
                        {
                            _rubricData[question.Id] = updated;
                            PopulateAIRubricContent(border, question);
                        }

                        ToastNotification.Show("Rubric updated successfully!", Application.Current.MainWindow);
                    }
                };

                var dangerBrush = TryFindResource("Status.Danger") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                var removeBtn = new Button
                {
                    Style = (Style)FindResource("Btn.Ghost"),
                    Padding = new System.Windows.Thickness(8, 6, 8, 6),
                    Cursor = Cursors.Hand,
                    ToolTip = "Remove and detach rubric"
                };
                var removeStack = new StackPanel { Orientation = Orientation.Horizontal };
                removeStack.Children.Add(new TextBlock
                {
                    Text = "🗑",
                    FontSize = 12,
                    Foreground = dangerBrush,
                    Margin = new System.Windows.Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                removeStack.Children.Add(new TextBlock
                {
                    Text = "Remove",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = dangerBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });
                removeBtn.Content = removeStack;

                removeBtn.Click += (s, e) =>
                {
                    var confirm = MessageBox.Show(
                        "Are you sure you want to remove and detach this AI rubric?",
                        "Confirm Detach Rubric",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirm == MessageBoxResult.Yes)
                    {
                        if (isSectionScope)
                        {
                            var targetSection = _sections.FirstOrDefault(sec => sec.Questions.Any(qu => qu.Id == question.Id));
                            if (targetSection != null)
                            {
                                foreach (var sq in targetSection.Questions.Where(qu => qu.Type == "Essay" || qu.Type == "Coding"))
                                {
                                    _rubricData.Remove(sq.Id);
                                }
                            }
                            UpdateQuestionsDisplay();
                        }
                        else
                        {
                            _rubricData.Remove(question.Id);
                            PopulateAIRubricContent(border, question);
                        }

                        ToastNotification.Show("Rubric detached.", Application.Current.MainWindow);
                    }
                };

                rightStack.Children.Add(editBtn);
                rightStack.Children.Add(removeBtn);

                grid.Children.Add(leftStack);
                grid.Children.Add(rightStack);

                border.Child = grid;
            }
        }

        // ─── Import exam questions from a .txt file ──────────────────
        // Populates _sections exactly like manually adding sections/
        // questions — nothing is saved to the database and nothing is
        // published. The imported draft just appears on this page for
        // the instructor to review, edit, and Publish when ready.
        private void ImportProgressOverlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        // ─── Import exam questions from a .txt file ──────────────────
        // Populates _sections asynchronously with real-time modal progress feedback.
        private async void ImportFromTxt_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteImportFileAsync();
        }

        public async Task ExecuteImportFileAsync(string? filePath = null)
        {
            if (IsImporting) return;

            if (string.IsNullOrEmpty(filePath))
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt",
                    Title = "Import Exam Questions from Text File"
                };

                if (dialog.ShowDialog() != true) return;
                filePath = dialog.FileName;
            }

            IsImporting = true;
            ImportStatusMessage = "Validating file...";

            var progress = new Progress<string>(msg => ImportStatusMessage = msg);

            try
            {
                await ImportFileInternalAsync(filePath, progress);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExamImport] Import failed: {ex}");
                MessageBox.Show(
                    $"Failed to import file:\n\n{ex.Message}\n\nPlease check your file or AI settings and try again.",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task ImportFileInternalAsync(string filePath, IProgress<string> progress)
        {
            // Phase 1: Validate file
            progress.Report("Validating file...");
            await Task.Run(() =>
            {
                if (!System.IO.File.Exists(filePath))
                    throw new System.IO.FileNotFoundException("The specified file does not exist.", filePath);

                var fileInfo = new System.IO.FileInfo(filePath);
                if (fileInfo.Length == 0)
                    throw new InvalidOperationException("The selected file is empty.");
            });

            // Phase 2: Read text content
            progress.Report("Reading text content...");
            string rawText = await Task.Run(() => System.IO.File.ReadAllText(filePath));
            if (string.IsNullOrWhiteSpace(rawText))
                throw new InvalidOperationException("The selected file contains no readable text.");

            // Phase 3: Parsing records
            progress.Report("Parsing records with AI...");
            List<ExamSection> importedSections;
            try
            {
                var geminiService = new GeminiExamImportService();
                var aiSections = await Task.Run(async () => await geminiService.ParseExamTextAsync(rawText));

                // Phase 4: Process exam structure
                progress.Report("Processing exam structure...");
                importedSections = await Task.Run(() => aiSections.SelectMany(ConvertImportedSection).ToList());
            }
            catch (Exception aiEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ExamImport] AI import failed: {aiEx.Message}. Attempting local parser fallback...");
                progress.Report("AI unavailable, falling back to local parser...");
                importedSections = await Task.Run(() => ParseExamTxtFile(filePath));
                if (importedSections == null || importedSections.Count == 0)
                    throw new InvalidOperationException($"AI Import failed ({aiEx.Message}) and local parser found no valid sections.", aiEx);
            }

            if (importedSections.Count == 0)
                throw new InvalidOperationException("No valid sections or questions were found in that file.");

            // Phase 5: Applying to draft
            progress.Report("Applying sections to exam draft...");
            _sections.Clear();
            _rubricData.Clear();

            for (int i = 0; i < importedSections.Count; i++)
            {
                var section = importedSections[i];
                section.OrderIndex = i + 1;
                foreach (var q in section.Questions)
                    q.SectionId = section.Id;

                _sections.Add(section);
            }

            foreach (var s in _sections)
                s.IsActive = false;

            var firstImported = _sections.First();
            firstImported.IsActive = true;
            _selectedSection = firstImported;

            SelectTypeButton(firstImported.Type);

            UpdateSectionList();
            UpdateQuestionsDisplay();
            UpdateActiveSectionHeader();

            progress.Report("Completed");

            int totalQuestions = importedSections.Sum(s => s.Questions.Count);
            ToastNotification.Show(
                $"Successfully imported {importedSections.Count} section(s) and {totalQuestions} question(s).",
                Application.Current.MainWindow);
        }

        private string DetermineQuestionType(ImportedQuestionDto q, string? sectionType)
        {
            if (!string.IsNullOrEmpty(q.QuestionType))
            {
                var pt = GetProperTypeName(q.QuestionType);
                if (pt == "Multiple Choice" || pt == "True or False" || pt == "Identification" ||
                    pt == "Enumeration" || pt == "Essay" || pt == "Coding")
                    return pt;
            }

            if ((q.Options != null && q.Options.Count >= 2) ||
                (!string.IsNullOrEmpty(q.OptionA) && !string.IsNullOrEmpty(q.OptionB)))
            {
                return "Multiple Choice";
            }

            if ((q.TestCases != null && q.TestCases.Count > 0) ||
                !string.IsNullOrWhiteSpace(q.StarterCode) ||
                !string.IsNullOrWhiteSpace(q.TargetLanguage) ||
                (!string.IsNullOrEmpty(q.Type) && GetProperTypeName(q.Type) == "Coding") ||
                (!string.IsNullOrWhiteSpace(q.ProgrammingLanguage) && _supportedLanguages.Contains(q.ProgrammingLanguage, StringComparer.OrdinalIgnoreCase)))
            {
                return "Coding";
            }

            if (q.IsTrue.HasValue ||
                (!string.IsNullOrEmpty(q.CorrectAnswer) && (q.CorrectAnswer.Trim().Equals("True", StringComparison.OrdinalIgnoreCase) || q.CorrectAnswer.Trim().Equals("False", StringComparison.OrdinalIgnoreCase))))
            {
                return "True or False";
            }

            if ((q.ExpectedAnswers != null && q.ExpectedAnswers.Count > 0) ||
                (!string.IsNullOrEmpty(q.Type) && GetProperTypeName(q.Type) == "Enumeration"))
            {
                return "Enumeration";
            }

            if (!string.IsNullOrEmpty(q.Rubric) ||
                (!string.IsNullOrEmpty(q.Type) && GetProperTypeName(q.Type) == "Essay"))
            {
                return "Essay";
            }

            if (!string.IsNullOrEmpty(sectionType))
            {
                var sType = GetProperTypeName(sectionType);
                if (sType == "Multiple Choice" || sType == "True or False" || sType == "Identification" ||
                    sType == "Enumeration" || sType == "Essay" || sType == "Coding")
                    return sType;
            }

            if (!string.IsNullOrEmpty(q.ExpectedAnswer) || !string.IsNullOrEmpty(q.CorrectAnswer))
            {
                return "Identification";
            }

            return "Essay";
        }

        private List<ExamSection> ConvertImportedSection(ImportedSectionDto dto)
        {
            var baseName = !string.IsNullOrWhiteSpace(dto.SectionTitle) ? dto.SectionTitle : (!string.IsNullOrWhiteSpace(dto.Name) ? dto.Name : "Imported Section");
            var instructions = dto.Instructions ?? string.Empty;

            var questionsByType = dto.Questions
                .Select(q => new
                {
                    Dto = q,
                    ProperType = DetermineQuestionType(q, dto.QuestionType)
                })
                .GroupBy(x => x.ProperType)
                .ToList();

            var result = new List<ExamSection>();

            if (questionsByType.Count == 0)
                return result;

            if (questionsByType.Count == 1)
            {
                var single = questionsByType[0];
                var section = new ExamSection
                {
                    Name = baseName,
                    Type = single.Key,
                    Instructions = instructions,
                    IsActive = false,
                    Questions = new List<QuestionItem>()
                };

                int order = 1;
                foreach (var item in single)
                {
                    section.Questions.Add(BuildQuestionItem(item.Dto, single.Key, order++));
                }
                result.Add(section);
                return result;
            }

            // Mixed question types: split into distinct homogeneous sections
            foreach (var group in questionsByType)
            {
                var section = new ExamSection
                {
                    Name = $"{baseName} ({group.Key})",
                    Type = group.Key,
                    Instructions = instructions,
                    IsActive = false,
                    Questions = new List<QuestionItem>()
                };

                int order = 1;
                foreach (var item in group)
                {
                    section.Questions.Add(BuildQuestionItem(item.Dto, group.Key, order++));
                }
                result.Add(section);
            }

            return result;
        }

        private QuestionItem BuildQuestionItem(ImportedQuestionDto q, string targetType, int order)
        {
            string rawPrompt = !string.IsNullOrWhiteSpace(q.Prompt) ? q.Prompt : (q.Text ?? string.Empty);

            // Clean leftover prefixes
            string cleanPrompt = System.Text.RegularExpressions.Regex.Replace(
                rawPrompt,
                @"^(?:Section\s+\d+[:\.\s]*|Part\s+[IVXLCDM\d]+[:\.\s—–\-]*|Question\s+\d+[:\.\s]*|Prompt:\s*|Points:\s*\d+[:\.\s]*)+",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

            string[] instructionsToStrip = new[]
            {
                "Select the single correct answer from the provided choices.",
                "List the required terms or concepts accurately.",
                "Provide detailed, structured technical explanations based on the prompt.",
                "Write robust, syntactically correct implementations in one of the supported languages: C, Python, or Java."
            };
            foreach (var inst in instructionsToStrip)
            {
                if (cleanPrompt.StartsWith(inst, StringComparison.OrdinalIgnoreCase))
                {
                    cleanPrompt = cleanPrompt.Substring(inst.Length).Trim();
                    cleanPrompt = System.Text.RegularExpressions.Regex.Replace(
                        cleanPrompt,
                        @"^(?:Section\s+\d+[:\.\s]*|Part\s+[IVXLCDM\d]+[:\.\s—–\-]*|Question\s+\d+[:\.\s]*|Prompt:\s*|Points:\s*\d+[:\.\s]*)+",
                        "",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                }
            }

            // Rubric extraction: if rubric is inside prompt, move it to q.Rubric
            int rubricIdx = cleanPrompt.IndexOf("Evaluation Rubric:", StringComparison.OrdinalIgnoreCase);
            if (rubricIdx >= 0)
            {
                if (string.IsNullOrWhiteSpace(q.Rubric))
                    q.Rubric = cleanPrompt.Substring(rubricIdx).Trim();
                cleanPrompt = cleanPrompt.Substring(0, rubricIdx).Trim();
            }

            string? optA = q.OptionA;
            string? optB = q.OptionB;
            string? optC = q.OptionC;
            string? optD = q.OptionD;

            if (targetType == "Multiple Choice")
            {
                if (q.Options != null && q.Options.Count > 0)
                {
                    var stripped = q.Options.Select(o =>
                        System.Text.RegularExpressions.Regex.Replace(o.Trim(), @"^\(?[A-Da-d]\)[\.\:\-\s]*", "").Trim()
                    ).ToList();
                    if (stripped.Count > 0 && string.IsNullOrEmpty(optA)) optA = stripped[0];
                    if (stripped.Count > 1 && string.IsNullOrEmpty(optB)) optB = stripped[1];
                    if (stripped.Count > 2 && string.IsNullOrEmpty(optC)) optC = stripped[2];
                    if (stripped.Count > 3 && string.IsNullOrEmpty(optD)) optD = stripped[3];
                }
            }

            string? correctAns = q.CorrectAnswer;
            if (targetType == "Multiple Choice")
            {
                correctAns = NormalizeOptionKey(correctAns, optA, optB, optC, optD);
            }

            string? targetLang = q.TargetLanguage ?? q.ProgrammingLanguage;
            if (!string.IsNullOrEmpty(targetLang) && _supportedLanguages.Contains(targetLang, StringComparer.OrdinalIgnoreCase))
            {
                targetLang = _supportedLanguages.First(l => l.Equals(targetLang, StringComparison.OrdinalIgnoreCase));
            }
            else if (targetType == "Coding")
            {
                targetLang = q.StarterCode?.Contains("#include") == true ? "C" : "Python";
            }

            int defaultPts = targetType == "Multiple Choice" ? 2 : (targetType == "Coding" ? 15 : (targetType == "Enumeration" ? 4 : (targetType == "True or False" ? 1 : 5)));

            var qItem = new QuestionItem
            {
                Prompt = cleanPrompt,
                Type = targetType,
                Points = q.Points > 0 ? q.Points : defaultPts,
                Order = order,
                CorrectAnswer = correctAns,
                OptionA = optA,
                OptionB = optB,
                OptionC = optC,
                OptionD = optD,
                ExpectedAnswer = q.ExpectedAnswer,
                AcceptedAnswers = q.ExpectedAnswers != null && q.ExpectedAnswers.Count > 0
                    ? new List<string>(q.ExpectedAnswers)
                    : new List<string>(),
                IsTrue = q.IsTrue,
                TargetLanguage = targetLang ?? "Python",
                Rubric = q.Rubric
            };

            // Explicit True or False Binding
            if (targetType == "True or False")
            {
                if (q.IsTrue.HasValue)
                {
                    qItem.IsTrue = q.IsTrue.Value;
                }
                else if (!string.IsNullOrWhiteSpace(correctAns))
                {
                    var ans = correctAns.Trim();
                    if (ans.Equals("True", StringComparison.OrdinalIgnoreCase) || ans.Equals("T", StringComparison.OrdinalIgnoreCase))
                        qItem.IsTrue = true;
                    else if (ans.Equals("False", StringComparison.OrdinalIgnoreCase) || ans.Equals("F", StringComparison.OrdinalIgnoreCase))
                        qItem.IsTrue = false;
                }
            }
            // Explicit Identification Binding
            else if (targetType == "Identification")
            {
                if (string.IsNullOrWhiteSpace(qItem.ExpectedAnswer) && !string.IsNullOrWhiteSpace(correctAns))
                {
                    qItem.ExpectedAnswer = correctAns.Trim();
                }
            }
            // Explicit Coding Question Binding
            else if (targetType == "Coding")
            {
                qItem.OptionA = null;
                qItem.OptionB = null;
                qItem.OptionC = null;
                qItem.OptionD = null;
                qItem.CorrectAnswer = null;

                if (!string.IsNullOrWhiteSpace(q.StarterCode))
                    qItem.StarterCodeStub = q.StarterCode.Trim();

                if (q.TestCases?.Any() == true)
                {
                    qItem.TestCases.Clear();
                    foreach (var tc in q.TestCases)
                    {
                        qItem.TestCases.Add(new TestCaseItem { Input = tc.Input ?? string.Empty, ExpectedOutput = tc.ExpectedOutput ?? string.Empty });
                    }
                }
            }

            // Explicit Essay Rubric Binding
            if (!string.IsNullOrWhiteSpace(q.Rubric))
            {
                _rubricData[qItem.Id] = new RubricData
                {
                    Description = q.Rubric,
                    AiInstructions = "Grade student response using the rubric criteria.",
                    TotalWeight = qItem.Points
                };
            }

            return qItem;
        }

        private List<ExamSection> ParseExamTxtFile(string filePath)
        {
            var lines = System.IO.File.ReadAllLines(filePath);
            var sections = new List<ExamSection>();

            ExamSection? currentSection = null;
            QuestionItem? currentQuestion = null;
            var mcOptions = new Dictionary<string, string>();
            int order = 0;
            string codingState = string.Empty;
            string currentLangBlock = string.Empty;

            void FlushQuestion()
            {
                if (currentQuestion == null || currentSection == null) return;

                if (!string.IsNullOrWhiteSpace(currentQuestion.Prompt))
                {
                    currentQuestion.Prompt = System.Text.RegularExpressions.Regex.Replace(
                        currentQuestion.Prompt,
                        @"^(?:Section\s+\d+[:\.\s]*|Question\s+\d+[:\.\s]*|Prompt:\s*|Points:\s*\d+[:\.\s]*)+",
                        "",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                }

                if (currentSection.Type == "Coding")
                {
                    currentQuestion.Type = "Coding";
                    if (string.IsNullOrWhiteSpace(currentQuestion.TargetLanguage))
                        currentQuestion.TargetLanguage = "C";
                    mcOptions.Clear();
                }

                if (string.IsNullOrEmpty(currentQuestion.Type))
                    currentQuestion.Type = DetectQuestionType(currentQuestion, mcOptions);

                ApplyOptionsForType(currentQuestion, mcOptions);

                if (!string.IsNullOrWhiteSpace(currentQuestion.StarterCodeStub))
                    currentQuestion.StarterCodeStub = currentQuestion.StarterCodeStub.Trim();

                if (!string.IsNullOrWhiteSpace(currentQuestion.Rubric))
                {
                    _rubricData[currentQuestion.Id] = new RubricData
                    {
                        Description = currentQuestion.Rubric.Trim(),
                        AiInstructions = "Grade student response using the rubric criteria.",
                        TotalWeight = currentQuestion.Points
                    };
                }

                currentQuestion.Order = order++;
                currentSection.Questions.Add(currentQuestion);
                currentQuestion = null;
                mcOptions = new Dictionary<string, string>();
                codingState = string.Empty;
                currentLangBlock = string.Empty;
            }

            ExamSection EnsureSection(string defaultName = "Imported Section", string defaultType = "Identification")
            {
                if (currentSection != null) return currentSection;

                currentSection = new ExamSection
                {
                    Name = defaultName,
                    Type = defaultType,
                    Instructions = string.Empty,
                    IsActive = false
                };
                sections.Add(currentSection);
                order = 0;
                return currentSection;
            }

            var sectionRegex = new System.Text.RegularExpressions.Regex(
                @"^(?:SECTION\b[:\s]*|(?:(?:Section|Part)\s+(?:\d+|[IVXLCDM]+)\b[\s:—–\.\-]*|(?:[IVXLCDM]+)[\.\:—–\-]\s+|\[.*?\])\s*)(.*)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var questionHeaderRegex = new System.Text.RegularExpressions.Regex(
                @"^(?:Q(?:\d+)?[:\.]\s*|Question\s*\d+\b[:\.\s]*)(.*)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var numberedItemRegex = new System.Text.RegularExpressions.Regex(
                @"^(\d+)[\.\)]\s+(.*)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var trimmed = lines[lineIndex].Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var sectionMatch = sectionRegex.Match(trimmed);
                var qHeaderMatch = questionHeaderRegex.Match(trimmed);

                // Section header
                if (sectionMatch.Success && !qHeaderMatch.Success)
                {
                    FlushQuestion();
                    var headerTitle = sectionMatch.Groups[1].Value.Trim();
                    headerTitle = System.Text.RegularExpressions.Regex.Replace(headerTitle, @"^[—–\-\:\.\s]+", "").Trim();
                    if (string.IsNullOrEmpty(headerTitle)) headerTitle = trimmed;

                    var detectedType = GetProperTypeName(headerTitle);
                    if (string.IsNullOrEmpty(detectedType) || detectedType == "Identification")
                        detectedType = GetProperTypeName(trimmed);

                    currentSection = new ExamSection
                    {
                        Name = trimmed,
                        Type = !string.IsNullOrEmpty(detectedType) ? detectedType : "Identification",
                        Instructions = string.Empty,
                        IsActive = false
                    };
                    sections.Add(currentSection);
                    order = 0;
                    continue;
                }

                // Explicit Question header e.g. "Question 1", "Question 3", "Question 6: Array Element...", "Q1: ..."
                if (qHeaderMatch.Success)
                {
                    FlushQuestion();
                    var sType = currentSection != null ? currentSection.Type : "Identification";
                    EnsureSection();

                    string qTitle = qHeaderMatch.Groups[1].Value.Trim();
                    currentQuestion = new QuestionItem
                    {
                        Prompt = qTitle,
                        Type = sType != "Identification" ? sType : string.Empty,
                        Points = sType == "Multiple Choice" ? 2 : (sType == "Coding" ? 15 : (sType == "Enumeration" ? 4 : 5)),
                        TargetLanguage = sType == "Coding" ? "C" : string.Empty
                    };
                    continue;
                }

                if (trimmed.StartsWith("INSTRUCTIONS:", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureSection().Instructions = trimmed.Substring("INSTRUCTIONS:".Length).Trim();
                    continue;
                }

                if (trimmed.StartsWith("POINTS:", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentQuestion != null &&
                        int.TryParse(trimmed.Substring("POINTS:".Length).Trim(), out int pts))
                        currentQuestion.Points = pts;
                    continue;
                }

                if (trimmed.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                {
                    var typeValue = GetProperTypeName(trimmed.Substring("TYPE:".Length).Trim());
                    if (currentQuestion != null)
                        currentQuestion.Type = typeValue;
                    else
                        EnsureSection().Type = typeValue;
                    continue;
                }

                // Multiple Choice Option: A) ... or * A) ... or A. ...
                bool isBlockedByCoding = currentSection?.Type == "Coding" && (codingState == "specification" || codingState == "startercode" || codingState == "testcases");
                var mcMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?:\*\s*)?\(?([A-Da-d])[\)\.\:\-]\s*(.*)$");
                if (mcMatch.Success && !isBlockedByCoding)
                {
                    var letter = mcMatch.Groups[1].Value.ToUpper();
                    if (letter == "A" && mcOptions.Count >= 2)
                    {
                        FlushQuestion();
                        EnsureSection("Multiple Choice Questions", "Multiple Choice");
                        currentQuestion = new QuestionItem
                        {
                            Type = "Multiple Choice",
                            Points = 2
                        };
                    }

                    mcOptions[letter] = mcMatch.Groups[2].Value.Trim();
                    if (currentQuestion != null) currentQuestion.Type = "Multiple Choice";
                    if (currentSection != null && currentSection.Type == "Identification")
                    {
                        currentSection.Type = "Multiple Choice";
                        currentSection.Name = "Multiple Choice Questions";
                    }
                    continue;
                }

                if (trimmed.StartsWith("ANSWERS:", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentQuestion != null)
                    {
                        var rawAns = trimmed.Substring("ANSWERS:".Length).Trim();
                        currentQuestion.AcceptedAnswers = rawAns.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        currentQuestion.Type = "Enumeration";
                    }
                    continue;
                }

                if (trimmed.StartsWith("ANSWER:", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentQuestion != null)
                    {
                        var value = trimmed.Substring("ANSWER:".Length).Trim();
                        currentQuestion.CorrectAnswer = value;
                        currentQuestion.ExpectedAnswer = value;

                        if (value.Equals("True", StringComparison.OrdinalIgnoreCase))
                            currentQuestion.IsTrue = true;
                        else if (value.Equals("False", StringComparison.OrdinalIgnoreCase))
                            currentQuestion.IsTrue = false;
                    }
                    continue;
                }

                // Enumeration answer bullets (e.g. 1. Encapsulation, * Encapsulation, - Encapsulation)
                bool isCurrentEnumeration = currentSection?.Type == "Enumeration" || currentQuestion?.Type == "Enumeration";
                var numItemMatch = numberedItemRegex.Match(trimmed);

                if (isCurrentEnumeration && currentQuestion != null && !string.IsNullOrWhiteSpace(currentQuestion.Prompt))
                {
                    if (numItemMatch.Success)
                    {
                        currentQuestion.AcceptedAnswers.Add(numItemMatch.Groups[2].Value.Trim());
                        continue;
                    }
                    if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
                    {
                        currentQuestion.AcceptedAnswers.Add(trimmed.Substring(2).Trim());
                        continue;
                    }
                }

                // Coding section handling: Problem Description, Starter Code, Test Cases, Signatures
                bool isCurrentCoding = currentSection?.Type == "Coding" || currentQuestion?.Type == "Coding";
                if (isCurrentCoding && currentQuestion != null)
                {
                    if (trimmed.StartsWith("Supported Languages:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("LANGUAGE:", StringComparison.OrdinalIgnoreCase))
                    {
                        string langVal = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                        var firstLang = langVal.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "C";
                        currentQuestion.TargetLanguage = _supportedLanguages.Contains(firstLang, StringComparer.OrdinalIgnoreCase)
                            ? _supportedLanguages.First(l => l.Equals(firstLang, StringComparison.OrdinalIgnoreCase))
                            : "C";
                        continue;
                    }

                    if (trimmed.StartsWith("Problem Description:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentQuestion.Prompt = trimmed.Substring("Problem Description:".Length).Trim();
                        codingState = "description";
                        continue;
                    }

                    if (trimmed.StartsWith("Specification", StringComparison.OrdinalIgnoreCase))
                    {
                        codingState = "specification";
                        continue;
                    }

                    if (codingState == "specification")
                    {
                        if (trimmed.StartsWith("Test Cases:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Starter Code:", StringComparison.OrdinalIgnoreCase))
                        {
                            // transition below
                        }
                        else
                        {
                            // ignore signature lines (* C: ... * Python: ...)
                            continue;
                        }
                    }

                    if (trimmed.StartsWith("Starter Code:", StringComparison.OrdinalIgnoreCase))
                    {
                        codingState = "startercode";
                        currentLangBlock = string.Empty;
                        continue;
                    }

                    if (trimmed.StartsWith("Test Cases:", StringComparison.OrdinalIgnoreCase))
                    {
                        codingState = "testcases";
                        continue;
                    }

                    if (codingState == "startercode")
                    {
                        if (trimmed.Equals("C:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Equals("Python:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Equals("Java:", StringComparison.OrdinalIgnoreCase))
                        {
                            currentLangBlock = trimmed.TrimEnd(':').ToUpperInvariant();
                            continue;
                        }

                        string targetUpper = (currentQuestion.TargetLanguage ?? "C").ToUpperInvariant();
                        if (currentLangBlock == targetUpper || string.IsNullOrEmpty(currentLangBlock))
                        {
                            currentQuestion.StarterCodeStub = (currentQuestion.StarterCodeStub ?? "") + lines[lineIndex] + "\n";
                        }
                        continue;
                    }

                    if (codingState == "testcases")
                    {
                        if (trimmed.StartsWith("Case", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Equals("Test Case", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Equals("Input", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Equals("Expected Output", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (trimmed.StartsWith("arr =") || trimmed.Contains("=") || trimmed.StartsWith("\""))
                        {
                            var nextIdx = lineIndex + 1;
                            while (nextIdx < lines.Length && string.IsNullOrWhiteSpace(lines[nextIdx])) nextIdx++;
                            if (nextIdx < lines.Length && !lines[nextIdx].Trim().StartsWith("Case", StringComparison.OrdinalIgnoreCase) && !lines[nextIdx].Trim().StartsWith("Starter Code", StringComparison.OrdinalIgnoreCase))
                            {
                                currentQuestion.TestCases.Add(new TestCaseItem
                                {
                                    Input = trimmed,
                                    ExpectedOutput = lines[nextIdx].Trim()
                                });
                                lineIndex = nextIdx;
                                continue;
                            }
                        }
                        continue;
                    }

                    if (codingState == "description")
                    {
                        currentQuestion.Prompt += " " + trimmed;
                        continue;
                    }
                }

                // Essay Rubric table handling
                if ((currentSection?.Type == "Essay" || currentQuestion?.Type == "Essay") && trimmed.StartsWith("Evaluation Rubric:", StringComparison.OrdinalIgnoreCase))
                {
                    codingState = "rubric";
                    currentQuestion!.Rubric = trimmed + "\n";
                    continue;
                }

                if (codingState == "rubric" && currentQuestion != null)
                {
                    currentQuestion.Rubric += lines[lineIndex] + "\n";
                    continue;
                }

                // If no question is active, check if this line is an unnumbered or numbered question prompt
                if (currentQuestion == null)
                {
                    // Avoid creating questions for preamble document headers
                    if (currentSection == null && (trimmed.Contains("Standard Assessment") || trimmed.Contains("This document serves")))
                        continue;

                    // Section instructions before Question 1
                    if (currentSection != null)
                    {
                        if (string.IsNullOrWhiteSpace(currentSection.Instructions))
                            currentSection.Instructions = trimmed;
                        else
                            currentSection.Instructions += " " + trimmed;
                        continue;
                    }

                    EnsureSection();
                    currentQuestion = new QuestionItem
                    {
                        Prompt = numItemMatch.Success ? numItemMatch.Groups[2].Value.Trim() : trimmed,
                        Type = currentSection != null && currentSection.Type != "Identification" ? currentSection.Type : string.Empty,
                        Points = 5
                    };
                    continue;
                }

                // If a new numbered question appears (e.g. "1.", "2.") and we aren't in enumeration or code blocks
                if (numItemMatch.Success && !isCurrentEnumeration && codingState != "startercode" && codingState != "testcases" && codingState != "rubric")
                {
                    FlushQuestion();
                    EnsureSection();
                    var sType = currentSection != null ? currentSection.Type : "Identification";
                    currentQuestion = new QuestionItem
                    {
                        Prompt = numItemMatch.Groups[2].Value.Trim(),
                        Type = sType != "Identification" ? sType : string.Empty,
                        Points = sType == "Multiple Choice" ? 2 : (sType == "Coding" ? 15 : (sType == "Enumeration" ? 4 : (sType == "True or False" ? 1 : 5))),
                        TargetLanguage = sType == "Coding" ? "Python" : string.Empty
                    };
                    continue;
                }

                // If options already accumulated and new line appears
                if (mcOptions.Count >= 2)
                {
                    FlushQuestion();
                    EnsureSection();
                    var sType = currentSection != null ? currentSection.Type : "Identification";
                    currentQuestion = new QuestionItem
                    {
                        Prompt = numItemMatch.Success ? numItemMatch.Groups[2].Value.Trim() : trimmed,
                        Type = sType != "Identification" ? sType : string.Empty,
                        Points = sType == "Multiple Choice" ? 2 : 5
                    };
                    continue;
                }

                // Continuation line of question prompt
                if (trimmed.StartsWith("Prompt:", StringComparison.OrdinalIgnoreCase))
                {
                    var pText = trimmed.Substring("Prompt:".Length).Trim();
                    if (string.IsNullOrWhiteSpace(currentQuestion.Prompt))
                        currentQuestion.Prompt = pText;
                    else
                        currentQuestion.Prompt += " " + pText;
                }
                else if (string.IsNullOrWhiteSpace(currentQuestion.Prompt))
                {
                    currentQuestion.Prompt = trimmed;
                }
                else
                {
                    currentQuestion.Prompt += " " + trimmed;
                }
            }

            FlushQuestion();

            // Enforce homogeneous sections: split any section containing multiple question types
            var finalSections = new List<ExamSection>();
            foreach (var sec in sections)
            {
                if (sec.Questions.Count == 0) continue;

                var groups = sec.Questions.GroupBy(q => !string.IsNullOrEmpty(q.Type) ? q.Type : sec.Type).ToList();
                if (groups.Count <= 1)
                {
                    if (groups.Count == 1 && groups[0].Key != sec.Type && !string.IsNullOrEmpty(groups[0].Key))
                        sec.Type = groups[0].Key;
                    finalSections.Add(sec);
                }
                else
                {
                    foreach (var g in groups)
                    {
                        var splitSec = new ExamSection
                        {
                            Name = $"{sec.Name} ({g.Key})",
                            Type = g.Key,
                            Instructions = sec.Instructions,
                            IsActive = false,
                            Questions = g.ToList()
                        };
                        int qOrder = 0;
                        foreach (var q in splitSec.Questions)
                            q.Order = qOrder++;
                        finalSections.Add(splitSec);
                    }
                }
            }

            return finalSections;
        }

        private string DetectQuestionType(QuestionItem question, Dictionary<string, string> mcOptions)
        {
            if (mcOptions.Count >= 2)
                return "Multiple Choice";

            if (!string.IsNullOrEmpty(question.ProgrammingLanguage))
                return "Coding";

            if (!string.IsNullOrEmpty(question.ExpectedAnswers))
                return "Enumeration";

            var answer = question.CorrectAnswer;
            if (!string.IsNullOrEmpty(answer) &&
                (answer.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                 answer.Equals("False", StringComparison.OrdinalIgnoreCase)))
                return "True or False";

            if (!string.IsNullOrEmpty(question.Type) && question.Type != "Identification")
                return question.Type;

            if (!string.IsNullOrEmpty(answer))
                return "Identification";

            if (!string.IsNullOrEmpty(question.Type))
                return question.Type;

            return "Essay";
        }

        private void ApplyOptionsForType(QuestionItem question, Dictionary<string, string> mcOptions)
        {
            if (question.Type == "Multiple Choice")
            {
                mcOptions.TryGetValue("A", out var a); question.OptionA = a;
                mcOptions.TryGetValue("B", out var b); question.OptionB = b;
                mcOptions.TryGetValue("C", out var c); question.OptionC = c;
                mcOptions.TryGetValue("D", out var d); question.OptionD = d;

                question.CorrectAnswer = NormalizeOptionKey(question.CorrectAnswer, a, b, c, d);
            }
            else if (question.Type == "Coding" && string.IsNullOrEmpty(question.ProgrammingLanguage))
            {
                question.ProgrammingLanguage = "C";
            }
        }

        public static string NormalizeOptionKey(string? raw, string? optA, string? optB, string? optC, string? optD)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var trimmed = raw.Trim();

            var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\(?([A-Da-d])(?:[\.\:\-\)]|\s|$)");
            if (match.Success)
                return match.Groups[1].Value.ToUpperInvariant();

            if (!string.IsNullOrEmpty(optA) && string.Equals(trimmed, optA.Trim(), StringComparison.OrdinalIgnoreCase)) return "A";
            if (!string.IsNullOrEmpty(optB) && string.Equals(trimmed, optB.Trim(), StringComparison.OrdinalIgnoreCase)) return "B";
            if (!string.IsNullOrEmpty(optC) && string.Equals(trimmed, optC.Trim(), StringComparison.OrdinalIgnoreCase)) return "C";
            if (!string.IsNullOrEmpty(optD) && string.Equals(trimmed, optD.Trim(), StringComparison.OrdinalIgnoreCase)) return "D";

            return trimmed;
        }

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToHomeStatic();
        }

        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToDashboardStatic();
        }

        private void NavigateToCreateExam(object sender, RoutedEventArgs e)
        {
            MainWindow.NavigateToCreateExamStatic();
        }
    }

    /// <summary>
    /// Custom Toast Notification - auto-dismisses after 3-5 seconds with fade animation
    /// </summary>
    public static class ToastNotification
    {
        private static Border _toastBorder;
        private static TextBlock _toastText;
        private static double _opacity = 1.0;
        private static System.Windows.Threading.DispatcherTimer _timer;
        private static bool _isFading = false;

        public static void Show(string message, Window parentWindow, int durationSeconds = 3, bool isWarning = false)
        {
            if (parentWindow == null) return;

            if (_toastBorder == null)
            {
                _toastBorder = new Border
                {
                    Background = isWarning ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) : new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20, 14, 20, 14),
                    MaxWidth = 500,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 20, 0, 0),
                    Opacity = 0,
                    RenderTransform = new TranslateTransform(0, -20)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _toastText = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(_toastText, 0);
                grid.Children.Add(_toastText);

                var closeBtn = new Button
                {
                    Content = "✕",
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Brushes.White,
                    FontSize = 16,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.7
                };
                closeBtn.Click += (s, e) => CloseToast();
                Grid.SetColumn(closeBtn, 1);
                grid.Children.Add(closeBtn);

                _toastBorder.Child = grid;

                var gridParent = parentWindow.Content as Grid;
                if (gridParent != null)
                {
                    gridParent.Children.Add(_toastBorder);
                }
                else
                {
                    var parentGrid = new Grid();
                    var oldContent = parentWindow.Content as UIElement;
                    parentGrid.Children.Add(oldContent);
                    parentGrid.Children.Add(_toastBorder);
                    parentWindow.Content = parentGrid;
                }
            }

            var toastGrid = _toastBorder.Child as Grid;
            if (toastGrid != null)
            {
                var textBlock = toastGrid.Children[0] as TextBlock;
                if (textBlock != null) textBlock.Text = message;
            }
            _toastBorder.Background = isWarning ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) : new SolidColorBrush(Color.FromRgb(34, 197, 94));

            _opacity = 1.0;
            _isFading = false;
            _toastBorder.Opacity = 0;
            _toastBorder.RenderTransform = new TranslateTransform(0, -20);
            _toastBorder.Visibility = Visibility.Visible;

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            var slideIn = new TranslateTransform();
            slideIn.Y = -20;
            _toastBorder.RenderTransform = slideIn;
            var slideAnimation = new System.Windows.Media.Animation.DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(300));
            slideIn.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
            _toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(durationSeconds);
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                FadeOutToast();
            };
            _timer.Start();
        }

        private static void FadeOutToast()
        {
            if (_toastBorder == null || _isFading) return;
            _isFading = true;

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            var slideOut = new TranslateTransform();
            slideOut.Y = 0;
            _toastBorder.RenderTransform = slideOut;
            var slideAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(500));
            slideOut.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
            _toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            fadeOut.Completed += (s, e) =>
            {
                _toastBorder.Visibility = Visibility.Collapsed;
                _isFading = false;
            };
        }

        private static void CloseToast()
        {
            if (_timer != null && _timer.IsEnabled)
                _timer.Stop();
            FadeOutToast();
        }
    }
}
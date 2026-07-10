using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ACPAMS.Services;
using ACPAMS.Models;

namespace ACPAMS.Views.Faculty
{
    public partial class CreateExamPage : UserControl
    {
        private List<ExamSection> _sections = new List<ExamSection>();
        private ExamSection _selectedSection;
        private string _selectedQuestionType = string.Empty;

        // Store rubric data per question
        private Dictionary<string, RubricData> _rubricData = new Dictionary<string, RubricData>();


        // Supported programming languages
        private readonly string[] _supportedLanguages = new[] { "Java", "C", "C#", "C++", "Python" };

        public class ExamSection
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; }
            public string Type { get; set; }
            public string Instructions { get; set; }
            public int QuestionCount => Questions.Count;
            public bool IsActive { get; set; }
            public List<QuestionItem> Questions { get; set; } = new List<QuestionItem>();
        }

        public class QuestionItem
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Text { get; set; }
            public int Points { get; set; }
            public string Type { get; set; }
            public int Order { get; set; }
            public string CorrectAnswer { get; set; }
            public string OptionA { get; set; }
            public string OptionB { get; set; }
            public string OptionC { get; set; }
            public string OptionD { get; set; }
            public string ExpectedAnswer { get; set; }
            public bool? IsTrue { get; set; }
            public string ProgrammingLanguage { get; set; }
            public string SectionId { get; set; }
            public string ExpectedAnswers { get; set; } // comma-separated for enumeration
        }

        public CreateExamPage()
        {
            InitializeComponent();

            _sections = new List<ExamSection>();
            _selectedSection = null;
            _selectedQuestionType = string.Empty;

            UpdateSectionList();
            UpdateQuestionsDisplay();
            UpdateActiveSectionHeader();
            ResetAllTypeButtons();
        }

        private void UpdateActiveSectionHeader()
        {
            var headerText = FindName("ActiveSectionHeader") as TextBlock;
            var totalDisplay = FindName("TotalQuestionsDisplay") as TextBlock;
            var typeDisplay = FindName("ActiveTypeDisplay") as TextBlock;

            if (_selectedSection != null)
            {
                if (headerText != null)
                    headerText.Text = $"{_selectedSection.Name} — {_selectedSection.Type}";
                if (totalDisplay != null)
                    totalDisplay.Text = $"Total: {_selectedSection.Questions.Sum(q => q.Points)} pts";
                if (typeDisplay != null)
                    typeDisplay.Text = _selectedSection.Type;
            }
            else
            {
                if (headerText != null)
                    headerText.Text = "No section selected";
                if (totalDisplay != null)
                    totalDisplay.Text = "Total: 0 pts";
                if (typeDisplay != null)
                    typeDisplay.Text = "—";
            }
        }

        private string GetProperTypeName(string type)
        {
            if (string.IsNullOrEmpty(type)) return string.Empty;

            var trimmed = type.Trim();
            var lowerType = trimmed.ToLower();

            if (lowerType.Contains("identification") || lowerType == "id")
                return "Identification";
            if (lowerType.Contains("multiple") && lowerType.Contains("choice"))
                return "Multiple Choice";
            if (lowerType.Contains("true") && lowerType.Contains("false"))
                return "True or False";
            if (lowerType == "essay")
                return "Essay";
            if (lowerType.Contains("enumeration") || lowerType == "enum")
                return "Enumeration";
            if (lowerType.Contains("coding") || lowerType == "code")
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

            var publishModal = new PublishConfirmModal();
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
                            SectionId = q.SectionId,
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
                        instructorId
                    );

                    ToastNotification.Show($"Exam '{exam.Title}' published successfully!",
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

            foreach (var section in _sections)
            {
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
                    Foreground = (Brush)FindResource("Text.Primary")
                };
                infoStack.Children.Add(nameText);

                var typeText = new TextBlock
                {
                    Text = section.Type,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("Text.Muted")
                };
                infoStack.Children.Add(typeText);

                grid.Children.Add(infoStack);

                var badge = new Border
                {
                    Background = (Brush)FindResource("Bg.Base"),
                    BorderBrush = (Brush)FindResource("Border.Medium"),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new System.Windows.Thickness(7, 2, 7, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(badge, 2);

                var countText = new TextBlock
                {
                    Text = section.Questions.Count.ToString(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("Text.Secondary")
                };
                badge.Child = countText;
                grid.Children.Add(badge);

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

            if (_selectedSection == null) return;

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

                var questionTextBox = new TextBox
                {
                    Text = q.Text,
                    Style = (Style)FindResource("Input.Text"),
                    Height = 60,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    Tag = q
                };
                questionTextBox.TextChanged += (s, e) =>
                {
                    (questionTextBox.Tag as QuestionItem).Text = questionTextBox.Text;
                };
                bodyStack.Children.Add(questionTextBox);

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

                        foreach (var lang in _supportedLanguages)
                        {
                            bool isSelected = (q.ProgrammingLanguage ?? "Java") == lang;
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
                            Text = "// Write your code here",
                            Style = (Style)FindResource("Input.Text"),
                            Height = 60,
                            FontFamily = new FontFamily("Consolas"),
                            AcceptsReturn = true,   
                            IsReadOnly = false
                        };
                        bodyStack.Children.Add(stubBox);

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
            bool isCorrect = (q.CorrectAnswer == letter);

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
                    newQuestion.ProgrammingLanguage = "Java";
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
                Background = (Brush)FindResource("AI.Bg"),
                BorderBrush = (Brush)FindResource("AI.Border"),
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new System.Windows.Thickness(14, 12, 14, 12),
                Margin = new System.Windows.Thickness(0, 16, 0, 0)
            };

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
                var button = s as Button;
                if (button == null) return;

                var q = button.Tag as QuestionItem;
                if (q == null) return;

                var rubricModal = new AIRubricModal { Owner = Application.Current.MainWindow };
                rubricModal.QuestionTitle = q.Text ?? "Question";
                rubricModal.QuestionId = q.Id;

                if (rubricModal.ShowDialog() == true)
                {
                    var rubricData = rubricModal.GetRubricData();

                    if (!_rubricData.ContainsKey(q.Id))
                    {
                        _rubricData[q.Id] = rubricData;
                    }
                    else
                    {
                        _rubricData[q.Id] = rubricData;
                    }

                    ToastNotification.Show("Rubric saved successfully!", Application.Current.MainWindow);
                }
            };
            Grid.SetColumn(btn, 1);
            grid.Children.Add(btn);

            border.Child = grid;
            return border;
        }

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Navigate(new FacultyHomePage());
        }

        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Navigate(new DashboardPage());
        }

        private void NavigateToCreateExam(object sender, RoutedEventArgs e)
        {
            // Already here
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ACPAMS.Views.Faculty
{
    public partial class CreateExamPage : UserControl
    {
        private List<ExamSection> _sections = new List<ExamSection>();
        private ExamSection _selectedSection;
        private string _selectedQuestionType = string.Empty;

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
            AddDefaultSections();

            if (_sections.Count > 3)
            {
                _selectedSection = _sections[3];
                _selectedSection.IsActive = true;
            }

            UpdateSectionList();
            UpdateQuestionsDisplay();
        }

        private void AddDefaultSections()
        {
            var defaultSections = new[]
            {
                new ExamSection {
                    Id = "default1",
                    Name = "Test I",
                    Type = "Identification",
                    IsActive = false,
                    Questions = new List<QuestionItem>()
                },
                new ExamSection {
                    Id = "default2",
                    Name = "Test II",
                    Type = "Multiple Choice",
                    IsActive = false,
                    Questions = new List<QuestionItem>()
                },
                new ExamSection {
                    Id = "default3",
                    Name = "Test III",
                    Type = "Essay",
                    IsActive = false,
                    Questions = new List<QuestionItem>()
                },
                new ExamSection {
                    Id = "default4",
                    Name = "Test IV",
                    Type = "Coding",
                    IsActive = true,
                    Questions = new List<QuestionItem>()
                }
            };

            defaultSections[3].Questions.Add(new QuestionItem
            {
                Id = "q1",
                Text = "Write a method that accepts an integer n and returns the nth Fibonacci number. Your solution must handle edge cases where n is 0 or negative.",
                Points = 50,
                Type = "Coding",
                Order = 1,
                ProgrammingLanguage = "Java"
            });

            defaultSections[0].Questions.Add(new QuestionItem
            {
                Id = "q2",
                Text = "What keyword is used in Java to define a constant variable that cannot be reassigned after initialization?",
                Points = 5,
                Type = "Identification",
                Order = 1,
                ExpectedAnswer = "final"
            });

            defaultSections[1].Questions.Add(new QuestionItem
            {
                Id = "q3",
                Text = "Which of the following is NOT a valid access modifier in Java?",
                Points = 3,
                Type = "Multiple Choice",
                Order = 1,
                OptionA = "package",
                OptionB = "private",
                OptionC = "protected",
                OptionD = "public",
                CorrectAnswer = "A"
            });

            defaultSections[2].Questions.Add(new QuestionItem
            {
                Id = "q4",
                Text = "Explain the concept of polymorphism in Object-Oriented Programming. Describe at least two real-world scenarios where it improves code design and maintainability.",
                Points = 20,
                Type = "Essay",
                Order = 1
            });

            _sections.AddRange(defaultSections);
        }

        private void AddSection_Click(object sender, RoutedEventArgs e)
        {
            var modal = new AddSectionModal();
            modal.Owner = Application.Current.MainWindow;

            if (modal.ShowDialog() == true)
            {
                var newSection = new ExamSection
                {
                    Name = modal.SectionName,
                    Type = modal.QuestionType,
                    Instructions = modal.Instructions,
                    IsActive = false,
                    Questions = new List<QuestionItem>()
                };

                _sections.Add(newSection);
                _selectedSection = newSection;
                newSection.IsActive = true;

                UpdateSectionList();
                UpdateQuestionsDisplay();

                MessageBox.Show($"Section '{newSection.Name}' added successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void UpdateSectionList()
        {
            var container = FindName("SectionsContainer") as StackPanel;
            if (container == null) return;

            container.Children.Clear();

            foreach (var section in _sections)
            {
                if (section.Id.StartsWith("default")) continue;

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

            UpdateSectionList();
            UpdateQuestionsDisplay();

            var headerText = FindName("ActiveSectionHeader") as TextBlock;
            if (headerText != null)
                headerText.Text = $"{section.Name} — {section.Type}";

            var totalDisplay = FindName("TotalQuestionsDisplay") as TextBlock;
            if (totalDisplay != null)
                totalDisplay.Text = $"Total: {section.Questions.Count} questions";

            var typeDisplay = FindName("ActiveTypeDisplay") as TextBlock;
            if (typeDisplay != null)
                typeDisplay.Text = section.Type;
        }

        private void UpdateQuestionsDisplay()
        {
            var container = FindName("QuestionsContainer") as StackPanel;
            if (container == null) return;

            container.Children.Clear();

            if (_selectedSection == null) return;

            var headerText = FindName("ActiveSectionHeader") as TextBlock;
            if (headerText != null)
                headerText.Text = $"{_selectedSection.Name} — {_selectedSection.Type}";

            var totalDisplay = FindName("TotalQuestionsDisplay") as TextBlock;
            if (totalDisplay != null)
                totalDisplay.Text = $"Total: {_selectedSection.Questions.Count} questions";

            var typeDisplay = FindName("ActiveTypeDisplay") as TextBlock;
            if (typeDisplay != null)
                typeDisplay.Text = _selectedSection.Type;

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

                // Editable question text
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

                // Render based on question type
                switch (q.Type)
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
                            Height = 34,
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
                        bodyStack.Children.Add(CreateAIRubricSection());
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
                                UpdateQuestionsDisplay();
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
                            IsReadOnly = true
                        };
                        bodyStack.Children.Add(stubBox);

                        bodyStack.Children.Add(CreateAIRubricSection());
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
                    Height = 34,
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
                Height = 34,
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
                MessageBox.Show("Please select a section first.",
                    "No Section Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ResetTypeButtons();

            button.Background = (Brush)FindResource("Accent.PrimaryBg");
            button.BorderBrush = (Brush)FindResource("Accent.Primary");
            button.BorderThickness = new System.Windows.Thickness(2);
            _selectedQuestionType = questionType;

            var newQuestion = new QuestionItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = $"New {questionType} Question - Edit me!",
                Points = 1,
                Type = questionType,
                Order = _selectedSection.Questions.Count + 1
            };

            switch (questionType)
            {
                case "Identification":
                    newQuestion.ExpectedAnswer = "final";
                    break;
                case "Multiple Choice":
                    newQuestion.OptionA = "Option A";
                    newQuestion.OptionB = "Option B";
                    newQuestion.OptionC = "Option C";
                    newQuestion.OptionD = "Option D";
                    newQuestion.CorrectAnswer = "A";
                    break;
                case "True or False":
                    newQuestion.IsTrue = true;
                    break;
                case "Essay":
                    break;
                case "Enumeration":
                    newQuestion.ExpectedAnswers = "Item 1, Item 2, Item 3, Item 4";
                    break;
                case "Coding":
                    newQuestion.ProgrammingLanguage = "Java";
                    break;
            }

            _selectedSection.Questions.Add(newQuestion);

            UpdateQuestionsDisplay();
            UpdateSectionList();

            MessageBox.Show($"New {questionType} question added to '{_selectedSection.Name}'!",
                "Question Added",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ResetTypeButtons()
        {
            var typeButtons = new[]
            {
                FindName("AddIdentificationBtn") as Button,
                FindName("AddMultipleChoiceBtn") as Button,
                FindName("AddTrueFalseBtn") as Button,
                FindName("AddEssayBtn") as Button,
                FindName("AddEnumerationBtn") as Button,
                FindName("AddCodingBtn") as Button
            };

            foreach (var btn in typeButtons)
            {
                if (btn != null)
                {
                    btn.Background = (Brush)FindResource("Bg.Surface");
                    btn.BorderBrush = (Brush)FindResource("Border.Subtle");
                    btn.BorderThickness = new System.Windows.Thickness(1);
                }
            }
        }

        // ========== FIXED: Uses the selected type from sidebar ==========
        private void AddNewQuestion_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSection == null)
            {
                MessageBox.Show("Please select a section first.",
                    "No Section Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Use the type selected from the sidebar (the one with the filled dot)
            // If none selected, fallback to the section's type
            var questionType = string.IsNullOrEmpty(_selectedQuestionType)
                ? _selectedSection.Type
                : _selectedQuestionType;

            MessageBox.Show($"Adding question of type: {questionType} to section: {_selectedSection.Name}");

            var newQuestion = new QuestionItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = $"New {questionType} Question - Edit me!",
                Points = 1,
                Type = questionType,
                Order = _selectedSection.Questions.Count + 1
            };

            switch (questionType)
            {
                case "Identification":
                    newQuestion.ExpectedAnswer = "final";
                    break;
                case "Multiple Choice":
                    newQuestion.OptionA = "Option A";
                    newQuestion.OptionB = "Option B";
                    newQuestion.OptionC = "Option C";
                    newQuestion.OptionD = "Option D";
                    newQuestion.CorrectAnswer = "A";
                    break;
                case "True or False":
                    newQuestion.IsTrue = true;
                    break;
                case "Essay":
                    break;
                case "Enumeration":
                    newQuestion.ExpectedAnswers = "Item 1, Item 2, Item 3, Item 4";
                    break;
                case "Coding":
                    newQuestion.ProgrammingLanguage = "Java";
                    break;
            }

            _selectedSection.Questions.Add(newQuestion);

            UpdateQuestionsDisplay();
            UpdateSectionList();

            MessageBox.Show($"New {questionType} question added to '{_selectedSection.Name}'!",
                "Question Added",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
                    }
                }
            }
        }

        private Brush GetTypeBadgeBackground(string type)
        {
            return type switch
            {
                "Coding" => (Brush)FindResource("Status.SuccessBg"),
                "Identification" => (Brush)FindResource("Bg.Base"),
                "Multiple Choice" => (Brush)FindResource("Accent.PrimaryBg"),
                "True or False" => (Brush)FindResource("Status.SuccessBg"),
                "Essay" => (Brush)FindResource("AI.Bg"),
                "Enumeration" => (Brush)FindResource("Status.WarningBg"),
                _ => (Brush)FindResource("Bg.Base")
            };
        }

        private Brush GetTypeBadgeBorder(string type)
        {
            return type switch
            {
                "Coding" => (Brush)FindResource("Status.Success"),
                "Identification" => (Brush)FindResource("Border.Medium"),
                "Multiple Choice" => (Brush)FindResource("Accent.Primary"),
                "True or False" => (Brush)FindResource("Status.Success"),
                "Essay" => (Brush)FindResource("AI.Border"),
                "Enumeration" => (Brush)FindResource("Status.Warning"),
                _ => (Brush)FindResource("Border.Medium")
            };
        }

        private Brush GetTypeBadgeForeground(string type)
        {
            return type switch
            {
                "Coding" => (Brush)FindResource("Status.Success"),
                "Identification" => (Brush)FindResource("Text.Secondary"),
                "Multiple Choice" => (Brush)FindResource("Accent.Primary"),
                "True or False" => (Brush)FindResource("Status.Success"),
                "Essay" => (Brush)FindResource("AI.Text"),
                "Enumeration" => (Brush)FindResource("Status.Warning"),
                _ => (Brush)FindResource("Text.Secondary")
            };
        }

        private Brush GetTypeColor(string type)
        {
            if (string.IsNullOrEmpty(type)) return (Brush)FindResource("QType.Identification");

            var lowerType = type.ToLower();
            if (lowerType.Contains("identification") || lowerType.Contains("id"))
                return (Brush)FindResource("QType.Identification");
            if (lowerType.Contains("multiple") || lowerType.Contains("choice"))
                return (Brush)FindResource("QType.MultipleChoice");
            if (lowerType.Contains("true") || lowerType.Contains("false"))
                return (Brush)FindResource("QType.TrueOrFalse");
            if (lowerType.Contains("essay"))
                return (Brush)FindResource("QType.Essay");
            if (lowerType.Contains("enumeration") || lowerType.Contains("enum"))
                return (Brush)FindResource("QType.Enumeration");
            if (lowerType.Contains("coding") || lowerType.Contains("code") || lowerType.Contains("java") || lowerType.Contains("c"))
                return (Brush)FindResource("QType.Coding");

            return (Brush)FindResource("QType.Identification");
        }

        private Border CreateAIRubricSection()
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
                VerticalAlignment = VerticalAlignment.Center
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
                rubricModal.ShowDialog();
            };
            Grid.SetColumn(btn, 1);
            grid.Children.Add(btn);

            border.Child = grid;
            return border;
        }

        private void NavigateToHome(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToHome();
        }

        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as Window1;
            mainWindow?.NavigateToDashboard();
        }

        private void NavigateToCreateExam(object sender, RoutedEventArgs e)
        {
            // Already here
        }
    }
}
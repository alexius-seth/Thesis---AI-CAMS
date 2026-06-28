using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ACPAMS.Views.Faculty
{
    public partial class CreateExamPage : UserControl
    {
        private List<ExamSection> _sections = new List<ExamSection>();
        private ExamSection _selectedSection;

        public class ExamSection
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; }
            public string Type { get; set; }
            public string Instructions { get; set; }
            public int QuestionCount { get; set; } = 0;
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
                    Type = "Coding (Java)",
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
                var border = new Border
                {
                    Style = (Style)FindResource("Card")
                };

                var innerBorder = new Border
                {
                    BorderBrush = GetTypeColor(q.Type),
                    BorderThickness = new System.Windows.Thickness(4, 0, 0, 0),
                    CornerRadius = new CornerRadius(0, 8, 8, 0)
                };

                var stack = new StackPanel();

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
                    Background = (Brush)FindResource("Status.SuccessBg"),
                    BorderBrush = (Brush)FindResource("Status.Success"),
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new System.Windows.Thickness(7, 2, 7, 2)
                };
                var typeBadgeText = new TextBlock
                {
                    Text = q.Type,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Status.Success")
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
                    IsReadOnly = true
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
                stack.Children.Add(headerBorder);

                var bodyStack = new StackPanel { Margin = new System.Windows.Thickness(18, 16, 18, 16) };

                var questionText = new TextBlock
                {
                    Text = q.Text,
                    FontSize = 13,
                    Foreground = (Brush)FindResource("Text.Primary"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(0, 0, 0, 8)
                };
                bodyStack.Children.Add(questionText);

                if (!string.IsNullOrEmpty(q.CorrectAnswer))
                {
                    var answerText = new TextBlock
                    {
                        Text = $"Correct Answer: {q.CorrectAnswer}",
                        FontSize = 12,
                        Foreground = (Brush)FindResource("Text.Secondary")
                    };
                    bodyStack.Children.Add(answerText);
                }

                if (!string.IsNullOrEmpty(q.ExpectedAnswer))
                {
                    var answerText = new TextBlock
                    {
                        Text = $"Expected Answer: {q.ExpectedAnswer}",
                        FontSize = 12,
                        Foreground = (Brush)FindResource("Text.Secondary")
                    };
                    bodyStack.Children.Add(answerText);
                }

                if (!string.IsNullOrEmpty(q.OptionA))
                {
                    var options = new List<string>();
                    if (!string.IsNullOrEmpty(q.OptionA)) options.Add($"A: {q.OptionA}");
                    if (!string.IsNullOrEmpty(q.OptionB)) options.Add($"B: {q.OptionB}");
                    if (!string.IsNullOrEmpty(q.OptionC)) options.Add($"C: {q.OptionC}");
                    if (!string.IsNullOrEmpty(q.OptionD)) options.Add($"D: {q.OptionD}");

                    if (options.Count > 0)
                    {
                        var optionsText = new TextBlock
                        {
                            Text = string.Join("  |  ", options),
                            FontSize = 12,
                            Foreground = (Brush)FindResource("Text.Secondary")
                        };
                        bodyStack.Children.Add(optionsText);
                    }
                }

                if (q.IsTrue.HasValue)
                {
                    var answerText = new TextBlock
                    {
                        Text = $"Answer: {(q.IsTrue.Value ? "True" : "False")}",
                        FontSize = 12,
                        Foreground = (Brush)FindResource("Text.Secondary")
                    };
                    bodyStack.Children.Add(answerText);
                }

                if (!string.IsNullOrEmpty(q.ProgrammingLanguage))
                {
                    var langText = new TextBlock
                    {
                        Text = $"Language: {q.ProgrammingLanguage}",
                        FontSize = 12,
                        Foreground = (Brush)FindResource("Text.Secondary")
                    };
                    bodyStack.Children.Add(langText);
                }

                innerBorder.Child = bodyStack;
                stack.Children.Add(innerBorder);

                border.Child = stack;
                container.Children.Add(border);
            }
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

        private void AddQuestion_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSection == null)
            {
                MessageBox.Show("Please select a section first.",
                    "No Section Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var newQuestion = new QuestionItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = "New Question - Edit me!",
                Points = 1,
                Type = _selectedSection.Type,
                Order = _selectedSection.Questions.Count + 1
            };

            _selectedSection.Questions.Add(newQuestion);
            UpdateQuestionsDisplay();
            UpdateSectionList();

            MessageBox.Show($"Question added to '{_selectedSection.Name}'!\n\nEdit the question text and points.",
                "Question Added",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            // Already here - do nothing
        }
    }
}
using ACPAMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ACPAMS.Views.Student.Canvas
{
    public partial class EnumerationCanvas : UserControl
    {
        private readonly List<TextBox> _answerBoxes = new();

        public EnumerationCanvas()
        {
            InitializeComponent();
        }

        // Called by StudentExamPage when loading a question
        public EnumerationCanvas(Question question, StudentAnswer? existingAnswer = null, int questionNumber = 1, int totalQuestions = 1)
        {
            InitializeComponent();

            if (TypeBadgeText != null)
                TypeBadgeText.Text = "Enumeration";

            if (PointsText != null)
                PointsText.Text = $"{(int)(question.Points ?? 1)} pts";

            if (QuestionNumberText != null)
                QuestionNumberText.Text = $"Question {questionNumber} of {totalQuestions}";

            if (QuestionText != null)
                QuestionText.Text = question.Text;

            // Determine item count from existing answers or expected answers
            var savedItems = (existingAnswer?.AnswerText ?? string.Empty)
                .Split('\n')
                .Select(s => s.Trim())
                .ToList();

            var expected = (question.CorrectAnswer ?? string.Empty)
                .Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            if (QuestionSubtext != null && expected.Count > 0)
            {
                decimal ptsPerItem = (question.Points ?? 0) / Math.Max(1, expected.Count);
                QuestionSubtext.Text = $"List {expected.Count} items in any order. Each correct item is worth {ptsPerItem:0.#} pts.";
            }

            int count = Math.Max(savedItems.Count(s => !string.IsNullOrEmpty(s)), Math.Max(expected.Count, 3));
            for (int i = 0; i < count; i++)
            {
                string text = i < savedItems.Count ? savedItems[i] : string.Empty;
                AddAnswerRow(text);
            }

            if (AddAnswerBtn != null)
                AddAnswerBtn.Click += (s, e) => AddAnswerRow(string.Empty);
        }

        private void AddAnswerRow(string initialText)
        {
            if (AnswersContainer == null) return;

            int index = _answerBoxes.Count + 1;
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var badgeBorder = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = (Brush)FindResource("Accent.Primary"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var numText = new TextBlock
            {
                Text = index.ToString(),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = numText;
            Grid.SetColumn(badgeBorder, 0);

            var inputBorder = new Border
            {
                Background = (Brush)FindResource("Bg.Surface"),
                BorderBrush = (Brush)FindResource("Border.Medium"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 40
            };
            var tb = new TextBox
            {
                Style = (Style)FindResource("Input.EnumItem"),
                Text = initialText
            };
            inputBorder.Child = tb;
            Grid.SetColumn(inputBorder, 1);

            grid.Children.Add(badgeBorder);
            grid.Children.Add(inputBorder);

            _answerBoxes.Add(tb);
            AnswersContainer.Children.Add(grid);
        }

        // Called by StudentExamPage to extract answers before navigating
        public string GetAnswer()
        {
            var answers = _answerBoxes
                .Select(b => b.Text.Trim())
                .Where(a => !string.IsNullOrEmpty(a));
            return string.Join("\n", answers);
        }
    }
}

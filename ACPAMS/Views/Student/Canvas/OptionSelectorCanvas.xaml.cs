using ACPAMS.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ACPAMS.Views.Student.Canvas
{
    public partial class OptionSelectorCanvas : UserControl
    {
        private string _selectedAnswer = string.Empty;
        private readonly List<Button> _optionButtons = new();

        public OptionSelectorCanvas()
        {
            InitializeComponent();
        }

        // Called by StudentExamPage when loading a question
        public OptionSelectorCanvas(Question question, StudentAnswer? existingAnswer = null)
        {
            InitializeComponent();

            QuestionText.Text = question.Text;
            PointsLabel.Text = $"{question.Points ?? 0} pts";

            if (existingAnswer?.AnswerText != null)
                _selectedAnswer = existingAnswer.AnswerText;

            if (question.QuestionType == "true_false")
            {
                TypeLabel.Text = "True or False";
                BuildOptions(new[] { "True", "False" });
            }
            else
            {
                TypeLabel.Text = "Multiple Choice";

                // Use real options from DB if available,
                // fall back to placeholder if faculty hasn't added options yet
                var options = question.Options
                    .OrderBy(o => o.OrderIndex)
                    .Select(o => o.OptionText)
                    .ToArray();

                if (options.Length == 0)
                    options = new[] { "No options added yet — faculty needs to add choices." };

                BuildOptions(options);
            }
        }

        // Dynamically builds option buttons with letter labels (A, B, C...)
        private void BuildOptions(string[] options)
        {
            OptionsPanel.Children.Clear();
            _optionButtons.Clear();

            for (int i = 0; i < options.Length; i++)
            {
                string letter = ((char)('A' + i)).ToString();
                string optionText = options[i];
                bool isSelected = _selectedAnswer == optionText;

                var btn = new Button
                {
                    Style = isSelected
                        ? (Style)FindResource("Btn.Option.Selected")
                        : (Style)FindResource("Btn.Option"),
                    Tag = optionText
                };

                btn.Content = BuildOptionContent(letter, optionText, isSelected);
                btn.Click += OptionButton_Click;

                _optionButtons.Add(btn);
                OptionsPanel.Children.Add(btn);
            }
        }

        // Builds the inner Grid (radio circle + letter + text) for one option
        private Grid BuildOptionContent(string letter, string text, bool isSelected)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var accentBrush = (SolidColorBrush)FindResource("Accent.Primary");
            var mutedBrush = (SolidColorBrush)FindResource("Text.Muted");
            var primaryBrush = (SolidColorBrush)FindResource("Text.Primary");
            var borderBrush = (SolidColorBrush)FindResource("Border.Medium");

            // Radio indicator
            UIElement radio;
            if (isSelected)
            {
                var radioGrid = new Grid
                {
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                radioGrid.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Stroke = accentBrush,
                    Fill = Brushes.Transparent
                });
                radioGrid.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 9,
                    Height = 9,
                    Fill = accentBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                radio = radioGrid;
            }
            else
            {
                radio = new System.Windows.Shapes.Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Stroke = borderBrush,
                    Fill = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            Grid.SetColumn(radio, 0);
            grid.Children.Add(radio);

            // Letter + text
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = letter,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = isSelected ? accentBrush : mutedBrush,
                Margin = new Thickness(0, 0, 0, 2)
            });
            textStack.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isSelected ? accentBrush : primaryBrush,
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            return grid;
        }

        // Handles selecting an option — only one can be selected at a time
        private void OptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;

            _selectedAnswer = clicked.Tag?.ToString() ?? string.Empty;

            // Rebuild all option contents to reflect the new selection
            for (int i = 0; i < _optionButtons.Count; i++)
            {
                var btn = _optionButtons[i];
                string letter = ((char)('A' + i)).ToString();
                string text = btn.Tag?.ToString() ?? string.Empty;
                bool isSelected = text == _selectedAnswer;

                btn.Style = isSelected
                    ? (Style)FindResource("Btn.Option.Selected")
                    : (Style)FindResource("Btn.Option");
                btn.Content = BuildOptionContent(letter, text, isSelected);
            }
        }

        // Called by StudentExamPage to extract the answer before navigating
        public string GetAnswer() => _selectedAnswer;
    }
}
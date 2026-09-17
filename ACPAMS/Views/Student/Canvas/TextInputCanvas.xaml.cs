using ACPAMS.Models;
using System.Windows.Controls;

namespace ACPAMS.Views.Student.Canvas
{
    public partial class TextInputCanvas : UserControl
    {
        public TextInputCanvas()
        {
            InitializeComponent();
        }

        // Called by StudentExamPage when loading a question
        public TextInputCanvas(Question question, StudentAnswer? existingAnswer = null, int questionNumber = 1, int totalQuestions = 1)
        {
            InitializeComponent();

            // Bind type badge
            string typeName = question.QuestionType?.ToLower() switch
            {
                "identification" => "Identification",
                "essay" => "Essay",
                _ => string.IsNullOrEmpty(question.QuestionType) ? "Question" : char.ToUpper(question.QuestionType[0]) + question.QuestionType.Substring(1)
            };
            if (TypeBadgeText != null)
                TypeBadgeText.Text = typeName;

            // Bind points
            if (PointsText != null)
                PointsText.Text = $"{(int)(question.Points ?? 1)} pts";

            // Bind question index
            if (QuestionNumberText != null)
                QuestionNumberText.Text = $"Question {questionNumber} of {totalQuestions}";

            // Bind question text
            if (QuestionText != null)
                QuestionText.Text = question.Text;

            // Restore previous answer if exists
            if (existingAnswer?.AnswerText != null && AnswerBox != null)
                AnswerBox.Text = existingAnswer.AnswerText;

            // Live word count
            if (AnswerBox != null)
            {
                UpdateWordCount(AnswerBox.Text);
                AnswerBox.TextChanged += (s, e) => UpdateWordCount(AnswerBox.Text);
            }
        }

        private void UpdateWordCount(string text)
        {
            if (WordCountText == null) return;
            if (string.IsNullOrWhiteSpace(text))
            {
                WordCountText.Text = "0 words";
                return;
            }
            var count = text.Split(new[] { ' ', '\r', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
            WordCountText.Text = $"{count} {(count == 1 ? "word" : "words")}";
        }

        // Called by StudentExamPage to extract the answer before navigating
        public string GetAnswer() => AnswerBox?.Text.Trim() ?? string.Empty;
    }
}

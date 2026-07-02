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
        public TextInputCanvas(Question question, StudentAnswer? existingAnswer = null)
        {
            InitializeComponent();

            // Bind question text
            if (QuestionText != null)
                QuestionText.Text = question.Text;

            // Restore previous answer if exists
            if (existingAnswer?.AnswerText != null && AnswerBox != null)
                AnswerBox.Text = existingAnswer.AnswerText;
        }

        // Called by StudentExamPage to extract the answer before navigating
        public string GetAnswer() => AnswerBox?.Text.Trim() ?? string.Empty;
    }
}

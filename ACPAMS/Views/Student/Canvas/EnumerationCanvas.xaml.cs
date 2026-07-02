using ACPAMS.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ACPAMS.Views.Student.Canvas
{
    public partial class EnumerationCanvas : UserControl
    {
        public EnumerationCanvas()
        {
            InitializeComponent();
        }

        // Called by StudentExamPage when loading a question
        public EnumerationCanvas(Question question, StudentAnswer? existingAnswer = null)
        {
            InitializeComponent();

            // Bind question text
            if (QuestionText != null)
                QuestionText.Text = question.Text;

            // Restore previous answers — stored as newline-separated string
            if (existingAnswer?.AnswerText != null)
            {
                var parts = existingAnswer.AnswerText.Split('\n');
                if (parts.Length > 0 && Answer1Box != null) Answer1Box.Text = parts[0];
                if (parts.Length > 1 && Answer2Box != null) Answer2Box.Text = parts[1];
                if (parts.Length > 2 && Answer3Box != null) Answer3Box.Text = parts[2];
            }
        }

        // Called by StudentExamPage to extract answers before navigating
        public string GetAnswer()
        {
            var answers = new List<string>();
            if (Answer1Box != null) answers.Add(Answer1Box.Text.Trim());
            if (Answer2Box != null) answers.Add(Answer2Box.Text.Trim());
            if (Answer3Box != null) answers.Add(Answer3Box.Text.Trim());
            return string.Join("\n", answers.Where(a => !string.IsNullOrEmpty(a)));
        }
    }
}

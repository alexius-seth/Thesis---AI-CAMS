using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ACPAMS.Views.Faculty
{
    /// <summary>
    /// Publish Confirmation Modal — shown when faculty clicks "Save and Publish" on CreateExamPage.
    /// </summary>
    public partial class PublishConfirmModal : Window
    {
        // Properties to hold the values entered by the user
        public string CourseCode { get; private set; } = string.Empty;
        public string ExamName { get; private set; } = string.Empty;
        public int TimeLimitMinutes { get; private set; } = 60;
        public string GeneratedExamCode { get; private set; } = string.Empty;

        public PublishConfirmModal()
        {
            InitializeComponent();

            // Wire up the Close button (X)
            var closeButton = FindName("CloseBtn") as Button;
            if (closeButton != null)
                closeButton.Click += (s, e) => CloseModal(false);

            // Wire up the Copy button
            var copyButton = FindName("CopyBtn") as Button;
            if (copyButton != null)
                copyButton.Click += CopyButton_Click;

            // Wire up the Finish and Publish button
            var publishButton = FindName("PublishBtn") as Button;
            if (publishButton != null)
                publishButton.Click += PublishButton_Click;

            // Wire up Course Code TextChanged event to generate exam code
            var courseCodeBox = FindName("CourseCodeBox") as TextBox;
            if (courseCodeBox != null)
            {
                courseCodeBox.TextChanged += CourseCode_TextChanged;
                // Initialize with current text
                CourseCode = courseCodeBox.Text.Trim();
                if (!string.IsNullOrEmpty(CourseCode))
                {
                    GenerateExamCode();
                }
            }

            // Make sure time limit box only accepts numbers
            var timeLimitBox = FindName("TimeLimitBox") as TextBox;
            if (timeLimitBox != null)
            {
                timeLimitBox.PreviewTextInput += (s, e) =>
                {
                    // Only allow digits
                    foreach (char c in e.Text)
                    {
                        if (!char.IsDigit(c))
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                };
            }

            // Wire up Enter key to trigger publish
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    PublishButton_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.Escape)
                {
                    CloseModal(false);
                }
            };

            // Focus the first input field
            if (courseCodeBox != null)
            {
                courseCodeBox.Focus();
                courseCodeBox.SelectAll();
            }
        }

        private void CourseCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            var courseCodeBox = sender as TextBox;
            if (courseCodeBox == null) return;

            CourseCode = courseCodeBox.Text.Trim();
            GenerateExamCode();
        }

        private void GenerateExamCode()
        {
            var generatedCodeBox = FindName("GeneratedCodeBox") as TextBox;
            if (generatedCodeBox == null) return;

            if (string.IsNullOrEmpty(CourseCode))
            {
                GeneratedExamCode = string.Empty;
                generatedCodeBox.Text = "Enter a course code to generate exam code";
                return;
            }

            // Generate a unique exam code: CourseCode + "-" + Date + "-" + Random
            var datePart = DateTime.Now.ToString("MMdd");
            var randomPart = new Random().Next(1000, 9999).ToString();
            GeneratedExamCode = $"{CourseCode}-{datePart}-{randomPart}";
            generatedCodeBox.Text = GeneratedExamCode;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(GeneratedExamCode))
            {
                ToastNotification.Show("Please enter a Course Code first to generate an exam code.",
                    Application.Current.MainWindow, 3, true);
                return;
            }

            try
            {
                Clipboard.SetText(GeneratedExamCode);
                ToastNotification.Show("Exam code copied to clipboard!",
                    Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                ToastNotification.Show("Failed to copy: " + ex.Message,
                    Application.Current.MainWindow, 3, true);
            }
        }

        private void PublishButton_Click(object sender, RoutedEventArgs e)
        {
            // Get all input values
            var courseCodeBox = FindName("CourseCodeBox") as TextBox;
            var timeLimitBox = FindName("TimeLimitBox") as TextBox;
            var examNameBox = FindName("ExamNameBox") as TextBox;

            // Validate Course Code
            CourseCode = courseCodeBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(CourseCode))
            {
                ToastNotification.Show("Please enter a Course Code.",
                    Application.Current.MainWindow, 3, true);
                courseCodeBox?.Focus();
                courseCodeBox?.SelectAll();
                return;
            }

            // Validate Time Limit
            if (!int.TryParse(timeLimitBox?.Text, out int timeLimit) || timeLimit < 1)
            {
                ToastNotification.Show("Please enter a valid Time Limit (minimum 1 minute).",
                    Application.Current.MainWindow, 3, true);
                timeLimitBox?.Focus();
                timeLimitBox?.SelectAll();
                return;
            }
            TimeLimitMinutes = timeLimit;

            // Validate Exam Name
            ExamName = examNameBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ExamName))
            {
                ToastNotification.Show("Please enter an Exam Name.",
                    Application.Current.MainWindow, 3, true);
                examNameBox?.Focus();
                examNameBox?.SelectAll();
                return;
            }

            // Validate Exam Code is generated
            if (string.IsNullOrEmpty(GeneratedExamCode))
            {
                GenerateExamCode();
                if (string.IsNullOrEmpty(GeneratedExamCode))
                {
                    ToastNotification.Show("Please enter a Course Code to generate the exam code.",
                        Application.Current.MainWindow, 3, true);
                    return;
                }
            }

            // All validations passed - close with success
            DialogResult = true;
            Close();
        }

        private void CloseModal(bool result)
        {
            DialogResult = result;
            Close();
        }

        // Helper method to get the generated exam code after dialog closes
        public string GetGeneratedExamCode() => GeneratedExamCode;
    }
}
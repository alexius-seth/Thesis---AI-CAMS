using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ACPAMS.Views.Faculty
{
    public partial class PublishConfirmModal : Window
    {
        public string CourseCode { get; private set; } = string.Empty;
        public string ExamName { get; private set; } = string.Empty;
        public int TimeLimitMinutes { get; private set; } = 60;
        public string GeneratedExamCode { get; private set; } = string.Empty;
        public bool RandomizeQuestions { get; private set; } = false;

        public PublishConfirmModal()
        {
            InitializeComponent();

            var closeButton = FindName("CloseBtn") as Button;
            if (closeButton != null)
                closeButton.Click += (s, e) => CloseModal(false);

            var copyButton = FindName("CopyBtn") as Button;
            if (copyButton != null)
                copyButton.Click += CopyButton_Click;

            var publishButton = FindName("PublishBtn") as Button;
            if (publishButton != null)
                publishButton.Click += PublishButton_Click;

            var courseCodeBox = FindName("CourseCodeBox") as TextBox;
            if (courseCodeBox != null)
            {
                courseCodeBox.TextChanged += CourseCode_TextChanged;
                CourseCode = courseCodeBox.Text.Trim();
                if (!string.IsNullOrEmpty(CourseCode))
                {
                    GenerateExamCode();
                }
            }

            var timeLimitBox = FindName("TimeLimitBox") as TextBox;
            if (timeLimitBox != null)
            {
                timeLimitBox.PreviewTextInput += (s, e) =>
                {
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
            var courseCodeBox = FindName("CourseCodeBox") as TextBox;
            var timeLimitBox = FindName("TimeLimitBox") as TextBox;
            var examNameBox = FindName("ExamNameBox") as TextBox;

            CourseCode = courseCodeBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(CourseCode))
            {
                ToastNotification.Show("Please enter a Course Code.",
                    Application.Current.MainWindow, 3, true);
                courseCodeBox?.Focus();
                courseCodeBox?.SelectAll();
                return;
            }

            if (!int.TryParse(timeLimitBox?.Text, out int timeLimit) || timeLimit < 1)
            {
                ToastNotification.Show("Please enter a valid Time Limit (minimum 1 minute).",
                    Application.Current.MainWindow, 3, true);
                timeLimitBox?.Focus();
                timeLimitBox?.SelectAll();
                return;
            }
            TimeLimitMinutes = timeLimit;

            ExamName = examNameBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ExamName))
            {
                ToastNotification.Show("Please enter an Exam Name.",
                    Application.Current.MainWindow, 3, true);
                examNameBox?.Focus();
                examNameBox?.SelectAll();
                return;
            }

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

            var randomizeBox = FindName("RandomizeQuestionsCheckBox") as CheckBox;
            RandomizeQuestions = randomizeBox?.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void CloseModal(bool result)
        {
            DialogResult = result;
            Close();
        }

        public string GetGeneratedExamCode() => GeneratedExamCode;
    }
}
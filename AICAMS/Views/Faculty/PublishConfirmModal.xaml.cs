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
        public string AcademicYear { get; private set; } = "2026-2027";
        public string Semester { get; private set; } = "1st Semester";
        public string Section { get; private set; } = string.Empty;
        public string ClassSchedule { get; private set; } = string.Empty;
        public bool RequiresApproval { get; private set; } = false;
        public DateTime? ScheduledStartTime { get; private set; }
        public DateTime? ScheduledEndTime { get; private set; }
        public string AutoArchiveRule { get; private set; } = "1HourAfter";
        public int AutoArchiveBufferMinutes { get; private set; } = 60;

        private const int DWMWA_NCRENDERING_POLICY = 2;
        private const int DWMNCRP_DISABLED = 1;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public PublishConfirmModal()
        {
            InitializeComponent();
            SourceInitialized += (s, e) =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int policy = DWMNCRP_DISABLED;
                DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));
            };

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

        public void OnCloseModalClicked(object sender, RoutedEventArgs e)
        {
            CloseModal(false);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseModal(false);
        }

        public PublishConfirmModal(
            string? examName,
            string? courseCode,
            int? timeLimitMinutes,
            bool? randomizeQuestions = null,
            string? academicYear = null,
            string? semester = null,
            string? section = null,
            string? classSchedule = null,
            bool? requiresApproval = null,
            DateTime? scheduledStartTime = null,
            DateTime? scheduledEndTime = null,
            string? autoArchiveRule = null,
            int? autoArchiveBufferMinutes = null) : this()
        {
            if (!string.IsNullOrEmpty(examName) && FindName("ExamNameBox") is TextBox enBox)
                enBox.Text = examName;

            if (!string.IsNullOrEmpty(courseCode) && FindName("CourseCodeBox") is TextBox ccBox)
                ccBox.Text = courseCode;

            if (timeLimitMinutes.HasValue && timeLimitMinutes.Value > 0 && FindName("TimeLimitBox") is TextBox tlBox)
                tlBox.Text = timeLimitMinutes.Value.ToString();

            if (randomizeQuestions.HasValue && FindName("RandomizeQuestionsCheckBox") is CheckBox cb)
                cb.IsChecked = randomizeQuestions.Value;

            if (!string.IsNullOrEmpty(academicYear) && FindName("AcademicYearBox") is TextBox ayBox)
                ayBox.Text = academicYear;

            if (!string.IsNullOrEmpty(semester) && FindName("SemesterComboBox") is ComboBox semBox)
            {
                foreach (ComboBoxItem item in semBox.Items)
                {
                    if (string.Equals(item.Content?.ToString(), semester, StringComparison.OrdinalIgnoreCase))
                    {
                        semBox.SelectedItem = item;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(section) && FindName("SectionBox") is TextBox secBox)
                secBox.Text = section;

            if (!string.IsNullOrEmpty(classSchedule) && FindName("ClassScheduleBox") is TextBox schedBox)
                schedBox.Text = classSchedule;

            if (requiresApproval.HasValue && FindName("RequiresApprovalCheckBox") is CheckBox reqCb)
                reqCb.IsChecked = requiresApproval.Value;

            if (scheduledStartTime.HasValue)
            {
                if (FindName("ScheduledStartDatePicker") is DatePicker sdp)
                    sdp.SelectedDate = scheduledStartTime.Value.Date;
                if (FindName("ScheduledStartTimeBox") is TextBox stBox)
                    stBox.Text = scheduledStartTime.Value.ToString("HH:mm");
            }

            if (scheduledEndTime.HasValue)
            {
                if (FindName("ScheduledEndDatePicker") is DatePicker edp)
                    edp.SelectedDate = scheduledEndTime.Value.Date;
                if (FindName("ScheduledEndTimeBox") is TextBox etBox)
                    etBox.Text = scheduledEndTime.Value.ToString("HH:mm");
            }

            if (!string.IsNullOrEmpty(autoArchiveRule) && FindName("AutoArchivePolicyComboBox") is ComboBox polBox)
            {
                int index = autoArchiveRule switch
                {
                    "2HoursAfter" => 1,
                    "EndOfDay" => 2,
                    "ManualOnly" => 3,
                    _ => 0
                };
                polBox.SelectedIndex = index;
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

        public void OnCopyExamCodeClicked(object sender, RoutedEventArgs e)
        {
            CopyButton_Click(sender, e);
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

        public void OnFinishAndPublishClicked(object sender, RoutedEventArgs e)
        {
            PublishButton_Click(sender, e);
        }

        private void PublishButton_Click(object sender, RoutedEventArgs e)
        {
            var courseCodeBox = FindName("CourseCodeBox") as TextBox;
            var timeLimitBox = FindName("TimeLimitBox") as TextBox;
            var examNameBox = FindName("ExamNameBox") as TextBox;
            var academicYearBox = FindName("AcademicYearBox") as TextBox;
            var semesterBox = FindName("SemesterComboBox") as ComboBox;
            var sectionBox = FindName("SectionBox") as TextBox;
            var classScheduleBox = FindName("ClassScheduleBox") as TextBox;
            var randomizeBox = FindName("RandomizeQuestionsCheckBox") as CheckBox;
            var requiresApprovalBox = FindName("RequiresApprovalCheckBox") as CheckBox;

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

            RandomizeQuestions = randomizeBox?.IsChecked == true;
            AcademicYear = !string.IsNullOrWhiteSpace(academicYearBox?.Text) ? academicYearBox.Text.Trim() : "2026-2027";
            Semester = (semesterBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1st Semester";
            Section = sectionBox?.Text?.Trim() ?? string.Empty;
            ClassSchedule = classScheduleBox?.Text?.Trim() ?? string.Empty;
            RequiresApproval = requiresApprovalBox?.IsChecked == true;

            var sdp = FindName("ScheduledStartDatePicker") as DatePicker;
            var stBox = FindName("ScheduledStartTimeBox") as TextBox;
            ScheduledStartTime = CombineDateAndTime(sdp, stBox);

            var edp = FindName("ScheduledEndDatePicker") as DatePicker;
            var etBox = FindName("ScheduledEndTimeBox") as TextBox;
            ScheduledEndTime = CombineDateAndTime(edp, etBox);

            var polBox = FindName("AutoArchivePolicyComboBox") as ComboBox;
            string selectedPolicy = (polBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1 Hour";
            if (selectedPolicy.Contains("Manual"))
            {
                AutoArchiveRule = "ManualOnly";
                AutoArchiveBufferMinutes = 0;
            }
            else if (selectedPolicy.Contains("2 Hours"))
            {
                AutoArchiveRule = "2HoursAfter";
                AutoArchiveBufferMinutes = 120;
            }
            else if (selectedPolicy.Contains("End of the Day"))
            {
                AutoArchiveRule = "EndOfDay";
                AutoArchiveBufferMinutes = 0;
            }
            else
            {
                AutoArchiveRule = "1HourAfter";
                AutoArchiveBufferMinutes = 60;
            }

            DialogResult = true;
            Close();
        }

        private DateTime? CombineDateAndTime(DatePicker? datePicker, TextBox? timeBox)
        {
            if (datePicker?.SelectedDate == null) return null;
            var date = datePicker.SelectedDate.Value.Date;
            var timeStr = timeBox?.Text?.Trim();
            if (!string.IsNullOrEmpty(timeStr) && TimeSpan.TryParse(timeStr, out var parsedSpan))
            {
                return date.Add(parsedSpan);
            }
            if (!string.IsNullOrEmpty(timeStr) && DateTime.TryParse(timeStr, out var parsedDateTime))
            {
                return date.Add(parsedDateTime.TimeOfDay);
            }
            return date;
        }

        private void CloseModal(bool result)
        {
            DialogResult = result;
            Close();
        }

        public string GetGeneratedExamCode() => GeneratedExamCode;
    }
}

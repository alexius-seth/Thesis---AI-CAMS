using ACPAMS.Data;
using ACPAMS.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ACPAMS.Views.Student
{
    public partial class StudentProfilePage : UserControl
    {
        public StudentProfilePage()
        {
            InitializeComponent();
            ACPAMS.MainWindow.Resize(520, 560);
            LoadStudentData();
        }

        private void LoadStudentData()
        {
            var student = SessionManager.CurrentStudent;
            if (student == null) return;

            StudentIdBox.Text = student.StudentId;
            FullNameBox.Text = string.IsNullOrWhiteSpace(student.MiddleInitial)
                ? $"{student.FirstName} {student.LastName}"
                : $"{student.FirstName} {student.MiddleInitial}. {student.LastName}";
            ProgramBox.Text = student.Program ?? "N/A";

            if (!string.IsNullOrWhiteSpace(student.YearLevel))
            {
                foreach (ComboBoxItem item in YearLevelBox.Items)
                {
                    if (string.Equals(item.Content?.ToString(), student.YearLevel, StringComparison.OrdinalIgnoreCase))
                    {
                        YearLevelBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = YearLevelBox.SelectedItem as ComboBoxItem;
            string newYearLevel = selectedItem?.Content?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newYearLevel))
            {
                MessageBox.Show("Please select a year level.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveBtn.IsEnabled = false;
            SaveBtn.Content = "Saving...";
            SuccessCard.Visibility = Visibility.Collapsed;

            try
            {
                var studentId = SessionManager.CurrentStudent?.StudentId;
                if (!string.IsNullOrEmpty(studentId))
                {
                    using var context = new AppDbContext();
                    var dbStudent = await context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
                    if (dbStudent != null)
                    {
                        dbStudent.YearLevel = newYearLevel;
                        await context.SaveChangesAsync();
                    }

                    if (SessionManager.CurrentStudent != null)
                    {
                        SessionManager.CurrentStudent.YearLevel = newYearLevel;
                    }

                    SuccessCard.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveBtn.IsEnabled = true;
                SaveBtn.Content = "Save Changes";
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            ACPAMS.MainWindow.Resize(520, 480);
            ACPAMS.MainWindow.Navigate(new ExamEntryPage());
        }
    }
}

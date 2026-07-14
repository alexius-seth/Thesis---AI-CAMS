using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ACPAMS.Data;
using ACPAMS.Services;
using ACPAMS.Views.Faculty.Tabs;

namespace ACPAMS.ViewModels
{
    public class FacultyHomePageViewModel : INotifyPropertyChanged
    {
        private readonly string _instructorId;

        // Guards against overlapping loads (e.g. if Loaded fires more than once).
        private bool _isLoadInFlight;

        private ObservableCollection<ExamStatusDto> _exams;
        private ExamStatusDto _selectedExam;
        private bool _isLoading;

        // Commands
        public ICommand MonitorExamCommand { get; set; }
        public ICommand EditExamCommand { get; set; }
        public ICommand ViewLogsCommand { get; set; }
        public ICommand ArchiveExamCommand { get; set; }
        public ICommand DeleteExamCommand { get; set; }
        public ICommand ViewReportCommand { get; set; }

        public ObservableCollection<ExamStatusDto> Exams
        {
            get => _exams;
            set
            {
                _exams = value;
                OnPropertyChanged();
            }
        }

        public ExamStatusDto SelectedExam
        {
            get => _selectedExam;
            set
            {
                _selectedExam = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNoExams));
            }
        }

        /// <summary>Drives the empty-state message on FacultyHomePage.</summary>
        public bool HasNoExams => !IsLoading && (Exams == null || Exams.Count == 0);

        public event PropertyChangedEventHandler PropertyChanged;

        public FacultyHomePageViewModel(string instructorId)
        {
            _instructorId = instructorId;
            Exams = new ObservableCollection<ExamStatusDto>();
            Exams.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasNoExams));

            MonitorExamCommand = new RelayCommand(param => OnMonitorExam(param as ExamStatusDto));
            EditExamCommand = new RelayCommand(param => OnEditExam(param as ExamStatusDto));
            ViewLogsCommand = new RelayCommand(param => OnViewLogs(param as ExamStatusDto));
            ArchiveExamCommand = new RelayCommand(async param =>
            {
                if (param is ExamStatusDto exam) await ArchiveExamAsync(exam.ExamId);
            });
            DeleteExamCommand = new RelayCommand(async param =>
            {
                if (param is ExamStatusDto exam) await DeleteExamAsync(exam.ExamId);
            });
        }

        // ── Each operation below opens its own short-lived AppDbContext. ──
        // DbContext instances are NOT safe to share across concurrent async
        // calls; reusing a single instance for the view model's whole
        // lifetime is what caused the "second operation was started on this
        // context" exception. A fresh context per call avoids that entirely.

        public async Task LoadExamsAsync()
        {
            if (_isLoadInFlight) return; // ignore re-entrant Loaded firing
            _isLoadInFlight = true;

            try
            {
                IsLoading = true;

                using var context = new AppDbContext();
                var examStatusService = new ExamStatusService(context);
                var exams = await examStatusService.GetExamsWithStatusAsync(_instructorId);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Exams.Clear();
                    foreach (var exam in exams)
                    {
                        Exams.Add(exam);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading exams: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                _isLoadInFlight = false;
            }
        }

        public async Task ArchiveExamAsync(Guid examId)
        {
            var result = MessageBox.Show(
                "Archive this exam? It will be hidden from the active list but not deleted.",
                "Confirm Archive", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var context = new AppDbContext())
                {
                    var examStatusService = new ExamStatusService(context);
                    await examStatusService.ArchiveExamAsync(examId);
                }

                await LoadExamsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving exam: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task DeleteExamAsync(Guid examId)
        {
            var result = MessageBox.Show("Are you sure you want to delete this exam? This action cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var context = new AppDbContext())
                {
                    var examStatusService = new ExamStatusService(context);
                    await examStatusService.DeleteExamAsync(examId);
                }

                await LoadExamsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting exam: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<int> GetActiveStudentsCountAsync(Guid examId)
        {
            using var context = new AppDbContext();
            var examStatusService = new ExamStatusService(context);
            return await examStatusService.GetActiveStudentsCountAsync(examId);
        }

        // ── Navigation actions (Live / Pending / Done card buttons) ────────
        // Monitor/ViewLogs now open MonitoringTab filtered to this exam's ID.
        // TODO: MainWindow.NavigateToCreateExamStatic() is still parameterless,
        // so "Edit Exam" opens the Create/Edit tab but doesn't yet pre-load
        // this specific exam. Add an examId-aware overload there when ready.

        private void OnMonitorExam(ExamStatusDto exam)
        {
            if (exam == null) return;
            ACPAMS.MainWindow.Navigate(new MonitoringTab(exam.ExamId));
        }

        private void OnEditExam(ExamStatusDto exam)
        {
            if (exam == null) return;
            try
            {
                ACPAMS.MainWindow.NavigateToCreateExamStatic();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}\n\n{ex.InnerException?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnViewLogs(ExamStatusDto exam)
        {
            if (exam == null) return;
            ACPAMS.MainWindow.Navigate(new MonitoringTab(exam.ExamId));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ACPAMS.Data;
using ACPAMS.Services;
using ACPAMS.Views.Faculty;
using ACPAMS.Views.Faculty.Tabs;

namespace ACPAMS.ViewModels
{
    public class FacultyHomePageViewModel : INotifyPropertyChanged
    {
        private readonly string _instructorId;
        private readonly bool _isArchivedView;

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
        public ICommand UnarchiveExamCommand { get; set; }
        public ICommand DeleteExamCommand { get; set; }
        public ICommand ViewReportCommand { get; set; }
        public ICommand ViewDashboardCommand { get; set; }
        public ICommand ViewScoresCommand { get; set; }

        public bool IsArchivedView => _isArchivedView;

        /// <summary>Single command for the "Recently Completed" row action button —
        /// branches to Review Submissions / Finalize Grades / View Report based on
        /// the exam's own ActionLabel (computed in ExamStatusService).</summary>
        public ICommand PrimaryActionCommand { get; set; }

        public ObservableCollection<ExamStatusDto> Exams
        {
            get => _exams;
            set
            {
                _exams = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CompletedExams));
                OnPropertyChanged(nameof(HasNoCompletedExams));
            }
        }

        /// <summary>Feeds the "Recently Completed" empty-state message.</summary>
        public bool HasNoCompletedExams => !CompletedExams.Any();

        /// <summary>Feeds the "Recently Completed — Needs Review" table (Section 3).</summary>
        public IEnumerable<ExamStatusDto> CompletedExams =>
            Exams?.Where(e => e.IsDone) ?? Enumerable.Empty<ExamStatusDto>();

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

        public FacultyHomePageViewModel(string instructorId, bool isArchivedView = false)
        {
            _instructorId = instructorId;
            _isArchivedView = isArchivedView;
            Exams = new ObservableCollection<ExamStatusDto>();
            Exams.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasNoExams));
                OnPropertyChanged(nameof(CompletedExams));
                OnPropertyChanged(nameof(HasNoCompletedExams));
            };

            MonitorExamCommand = new RelayCommand(param => OnMonitorExam(param as ExamStatusDto));
            EditExamCommand = new RelayCommand(param => OnEditExam(param as ExamStatusDto));
            ViewLogsCommand = new RelayCommand(param => OnViewLogs(param as ExamStatusDto));
            ViewDashboardCommand = new RelayCommand(param => OnViewDashboard(param as ExamStatusDto));
            ViewScoresCommand = new RelayCommand(param => OnViewScores(param as ExamStatusDto));
            ViewReportCommand = new RelayCommand(param => OnViewReport(param as ExamStatusDto));
            PrimaryActionCommand = new RelayCommand(param =>
            {
                if (param is ExamStatusDto exam) OnPrimaryActionAsync(exam);
            });
            ArchiveExamCommand = new RelayCommand(async param =>
            {
                if (param is ExamStatusDto exam) await ArchiveExamAsync(exam.ExamId);
            });
            UnarchiveExamCommand = new RelayCommand(async param =>
            {
                if (param is ExamStatusDto exam) await UnarchiveExamAsync(exam.ExamId);
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
                var exams = await examStatusService.GetExamsWithStatusAsync(_instructorId, archivedOnly: _isArchivedView);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Exams.Clear();
                    foreach (var exam in exams)
                    {
                        Exams.Add(exam);
                    }
                    OnPropertyChanged(nameof(CompletedExams));
                    OnPropertyChanged(nameof(HasNoCompletedExams));
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

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var card = Exams.FirstOrDefault(e => e.ExamId == examId);
                    if (card != null) Exams.Remove(card);
                });

                await LoadExamsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving exam: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task UnarchiveExamAsync(Guid examId)
        {
            var result = MessageBox.Show(
                "Restore this exam? It will be moved back to the active assessments list.",
                "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var context = new AppDbContext())
                {
                    var examStatusService = new ExamStatusService(context);
                    await examStatusService.UnarchiveExamAsync(examId);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var card = Exams.FirstOrDefault(e => e.ExamId == examId);
                    if (card != null) Exams.Remove(card);
                });

                await LoadExamsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring exam: {ex.Message}", "Error",
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
                ACPAMS.MainWindow.Navigate(new CreateExamPage(exam.ExamId));
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

        // "View Dashboard" — item 4: every Active/Upcoming card gets this button,
        // opening the Dashboard scoped to that specific exam (not just "whatever's live").
        private void OnViewDashboard(ExamStatusDto exam)
        {
            if (exam == null) return;
            ACPAMS.MainWindow.Navigate(new DashboardPage(exam.ExamId));
        }

        // "View Scores" — dedicated per-exam scores view (ExamScoresPage), showing
        // every student's score broken down by section, with a way to jump back
        // into GradingTab for anything not yet finalized. Makes sense for
        // Active/Upcoming AND Recently Completed exams alike — a live exam can
        // still have some already-submitted, already-graded attempts worth checking.
        private void OnViewScores(ExamStatusDto exam)
        {
            if (exam == null) return;
            ACPAMS.MainWindow.Navigate(new ExamScoresPage(exam.ExamId));
        }

        // "View Report" — used both as its own command and as the terminal state
        // of PrimaryActionCommand for fully-graded / fully-objective exams.
        // Scoped to THIS exam only, so students from other subjects don't show up.
        private void OnViewReport(ExamStatusDto exam)
        {
            if (exam == null) return;
            ACPAMS.MainWindow.Navigate(new ReportsTab(exam.ExamId));
        }

        // Item 5: dynamic action for the "Recently Completed" table.
        // exam.ActionLabel is computed server-side in ExamStatusService, so
        // this just has to route to the right place per label.
        //
        // IMPORTANT: "Finalize Grades" does NOT finalize anything from here.
        // It used to call FinalizeGradesAsync directly — a single click would
        // lock in every score with no chance to actually look at any answer
        // first. Both this state and "Review Submissions" now open GradingTab;
        // the actual Finalize action lives there, as something the instructor
        // does only after reviewing, not as a blind one-click from a list.
        private void OnPrimaryActionAsync(ExamStatusDto exam)
        {
            switch (exam.ActionLabel)
            {
                case "Review Submissions":
                case "Finalize Grades":
                    ACPAMS.MainWindow.Navigate(new GradingTab(exam.ExamId));
                    break;

                case "View Report":
                default:
                    OnViewReport(exam);
                    break;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

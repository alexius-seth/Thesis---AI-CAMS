using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ACPAMS.Services;

namespace ACPAMS.ViewModels
{
    public class ExamCardViewModel : ExamStatusDto
    {
        public ExamCardViewModel() { }

        public ExamCardViewModel(ExamStatusDto dto)
        {
            ExamId = dto.ExamId;
            Title = dto.Title;
            ExamCode = dto.ExamCode;
            CourseCode = dto.CourseCode;
            Type = dto.Type;
            Status = dto.Status;
            StatusDisplay = dto.StatusDisplay;
            ActiveStudents = dto.ActiveStudents;
            TotalStudents = dto.TotalStudents;
            CreatedAt = dto.CreatedAt;
            DurationMinutes = dto.DurationMinutes;
            TotalPoints = dto.TotalPoints;
            HasAttempts = dto.HasAttempts;
            Attempts = dto.Attempts;
            HasSubjectiveQuestions = dto.HasSubjectiveQuestions;
            PendingReviewCount = dto.PendingReviewCount;
            ReadyToFinalizeCount = dto.ReadyToFinalizeCount;
            ActionLabel = dto.ActionLabel;
            GradingStatusText = dto.GradingStatusText;
            GradingStatusIsWarning = dto.GradingStatusIsWarning;
            ArchivedDate = dto.ArchivedDate ?? dto.CreatedAt;
            AcademicYear = dto.AcademicYear;
            Semester = dto.Semester;
            Section = dto.Section;
            ClassSchedule = dto.ClassSchedule;
            RequiresApproval = dto.RequiresApproval;
        }
    }

    public class ArchivedExamsViewModel : FacultyHomePageViewModel
    {
        private List<ExamCardViewModel> _allArchivedExams = new();

        public ObservableCollection<ExamCardViewModel> FilteredArchivedExams { get; set; } = new();

        private string _selectedDateFilter = "All Time";
        public string SelectedDateFilter
        {
            get => _selectedDateFilter;
            set
            {
                if (_selectedDateFilter != value)
                {
                    _selectedDateFilter = value;
                    OnPropertyChanged(nameof(SelectedDateFilter));
                    OnPropertyChanged(nameof(IsCustomRangeVisible));
                    ApplyFilters();
                }
            }
        }

        public bool IsCustomRangeVisible => SelectedDateFilter == "Custom Range";

        private DateTime? _startDate;
        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged(nameof(StartDate));
                    ApplyFilters();
                }
            }
        }

        private DateTime? _endDate;
        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate != value)
                {
                    _endDate = value;
                    OnPropertyChanged(nameof(EndDate));
                    ApplyFilters();
                }
            }
        }

        public new bool HasNoExams => !IsLoading && (FilteredArchivedExams == null || FilteredArchivedExams.Count == 0);

        public ArchivedExamsViewModel(string instructorId) : base(instructorId, isArchivedView: true)
        {
            Exams.CollectionChanged += (s, e) =>
            {
                _allArchivedExams = Exams.Select(x => new ExamCardViewModel(x)).ToList();
                ApplyFilters();
            };
        }

        public async Task LoadArchivedExamsAsync()
        {
            await LoadExamsAsync();
            _allArchivedExams = Exams.Select(x => new ExamCardViewModel(x)).ToList();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allArchivedExams == null) return;

            IEnumerable<ExamCardViewModel> query = _allArchivedExams;
            DateTime now = DateTime.Now;

            switch (SelectedDateFilter)
            {
                case "Last 7 Days":
                    query = query.Where(e => (e.ArchivedDate ?? e.DateCreated) >= now.AddDays(-7));
                    break;

                case "Last 30 Days":
                    query = query.Where(e => (e.ArchivedDate ?? e.DateCreated) >= now.AddDays(-30));
                    break;

                case "Last 90 Days":
                    query = query.Where(e => (e.ArchivedDate ?? e.DateCreated) >= now.AddDays(-90));
                    break;

                case "Custom Range":
                    if (StartDate.HasValue)
                        query = query.Where(e => (e.ArchivedDate ?? e.DateCreated).Date >= StartDate.Value.Date);
                    if (EndDate.HasValue)
                        query = query.Where(e => (e.ArchivedDate ?? e.DateCreated).Date <= EndDate.Value.Date);
                    break;

                case "All Time":
                default:
                    break;
            }

            FilteredArchivedExams.Clear();
            foreach (var exam in query)
            {
                FilteredArchivedExams.Add(exam);
            }

            OnPropertyChanged(nameof(HasNoExams));
        }
    }
}


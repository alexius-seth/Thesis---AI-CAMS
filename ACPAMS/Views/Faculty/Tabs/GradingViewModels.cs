using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ACPAMS.Views.Faculty.Tabs
{
    // One row in the right-hand answer viewer — one question + the student's answer to it.
    public class QuestionAnswerVM : INotifyPropertyChanged
    {
        public Guid AnswerId { get; set; }        // student_answers.answer_id
        public Guid QuestionId { get; set; }
        public int QuestionNumber { get; set; }    // 1-based, set by the loader (OrderIndex position)
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }   // "MCQ", "TrueFalse", "Identification", "Essay", "Enumeration", "Coding"
        public string StudentAnswerText { get; set; }
        public int MaxPoints { get; set; }         // questions.points (assumes this column exists; else default 10)

        private int? _obtainedPoints;
        public int? ObtainedPoints
        {
            get => _obtainedPoints;
            set
            {
                if (_obtainedPoints == value) return;
                _obtainedPoints = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGraded));
            }
        }

        public bool IsGraded => ObtainedPoints.HasValue;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // One row in the left queue — one student's attempt.
    public class AttemptQueueItemVM : INotifyPropertyChanged
    {
        public Guid AttemptId { get; set; }        // student_exam_attempts.attempt_id
        public string StudentName { get; set; }
        public string StudentInitials { get; set; }
        public string PcLabel { get; set; }        // whatever seat/PC label you track, or student_id fallback
        public string SectionName { get; set; }
        public bool IsSubmitted { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private bool _isFullyGraded;
        public bool IsFullyGraded
        {
            get => _isFullyGraded;
            set { _isFullyGraded = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // A section group of attempts, for the left panel's grouped list.
    public class SectionGroupVM
    {
        public string SectionName { get; set; }        // "BSCS-3A", or "Unassigned"
        public ObservableCollection<AttemptQueueItemVM> Attempts { get; set; } = new();
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ACPAMS.Models;

namespace ACPAMS.ViewModels
{
    public class ExamQuestionViewModel : INotifyPropertyChanged
    {
        public Question Question { get; }
        public int Index { get; }
        public int DisplayNumber => Index + 1;
        public string SectionName => !string.IsNullOrWhiteSpace(Question?.SectionName) ? Question.SectionName : "General";

        private bool _isFlaggedForReview;
        public bool IsFlaggedForReview
        {
            get => _isFlaggedForReview;
            set
            {
                if (_isFlaggedForReview != value)
                {
                    _isFlaggedForReview = value;
                    OnPropertyChanged(nameof(IsFlaggedForReview));
                    OnPropertyChanged(nameof(IsAnswered));
                    OnPropertyChanged(nameof(IsUnanswered));
                }
            }
        }

        private string? _draftText;
        public string? DraftText
        {
            get => _draftText;
            set
            {
                if (_draftText != value)
                {
                    _draftText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasContent));
                    OnPropertyChanged(nameof(IsAnswered));
                    OnPropertyChanged(nameof(IsUnanswered));
                }
            }
        }

        private string? _draftCode;
        public string? DraftCode
        {
            get => _draftCode;
            set
            {
                if (_draftCode != value)
                {
                    _draftCode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasContent));
                    OnPropertyChanged(nameof(IsAnswered));
                    OnPropertyChanged(nameof(IsUnanswered));
                }
            }
        }

        public bool HasContent => !string.IsNullOrWhiteSpace(DraftText) || !string.IsNullOrWhiteSpace(DraftCode);
        public bool IsAnswered => HasContent && !IsFlaggedForReview;
        public bool IsUnanswered => !HasContent && !IsFlaggedForReview;

        public ExamQuestionViewModel(Question question, int index, string? initialText = null, string? initialCode = null)
        {
            Question = question;
            Index = index;
            _draftText = initialText;
            _draftCode = initialCode;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


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
        public decimal MaxPoints { get; set; }         // questions.points (assumes this column exists; else default 10)

        public string? ExpectedAnswer { get; set; }
        public bool HasExpectedAnswer => !string.IsNullOrWhiteSpace(ExpectedAnswer);

        public bool IsSubjective => QuestionType != null &&
            (QuestionType.StartsWith("Essay", StringComparison.OrdinalIgnoreCase) ||
             QuestionType.StartsWith("Coding", StringComparison.OrdinalIgnoreCase));

        public bool IsObjective => !IsSubjective;

        public string ReferenceFontFamily =>
            QuestionType != null && QuestionType.StartsWith("Coding", StringComparison.OrdinalIgnoreCase)
                ? "Consolas"
                : "Segoe UI";

        private string _matchStatus = string.Empty;
        public string MatchStatus
        {
            get => _matchStatus;
            set
            {
                if (_matchStatus == value) return;
                _matchStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMatchStatus));
                OnPropertyChanged(nameof(IsMatchCorrect));
                OnPropertyChanged(nameof(IsMatchFuzzy));
                OnPropertyChanged(nameof(IsMatchIncorrect));
            }
        }

        public bool HasMatchStatus => !string.IsNullOrWhiteSpace(MatchStatus);
        public bool IsMatchCorrect => string.Equals(MatchStatus, "Correct", StringComparison.OrdinalIgnoreCase);
        public bool IsMatchFuzzy => string.Equals(MatchStatus, "Fuzzy / Near Match", StringComparison.OrdinalIgnoreCase);
        public bool IsMatchIncorrect => string.Equals(MatchStatus, "Incorrect", StringComparison.OrdinalIgnoreCase);

        private decimal? _originalPoints;
        private bool _isOverridden;
        public bool IsOverridden
        {
            get => _isOverridden;
            set
            {
                if (_isOverridden == value) return;
                _isOverridden = value;
                OnPropertyChanged();
            }
        }

        public Action? OnScoreChangedAction { get; set; }

        public void InitializePoints(decimal? points)
        {
            _originalPoints = points;
            _obtainedPoints = points;
            _scoreInputText = points?.ToString("0.##") ?? string.Empty;
            _isOverridden = false;
        }

        private string _scoreInputText = string.Empty;
        public string ScoreInputText
        {
            get => _scoreInputText;
            set
            {
                if (_scoreInputText == value) return;
                _scoreInputText = value;
                OnPropertyChanged();

                if (string.IsNullOrWhiteSpace(value))
                {
                    _obtainedPoints = null;
                    if (_originalPoints != null) IsOverridden = true;
                }
                else if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal parsed) ||
                         decimal.TryParse(value, out parsed))
                {
                    if (parsed < 0) parsed = 0;
                    if (parsed > MaxPoints) parsed = MaxPoints;

                    _obtainedPoints = parsed;
                    if (_originalPoints == null || _originalPoints.Value != parsed)
                    {
                        IsOverridden = true;
                    }
                }

                OnPropertyChanged(nameof(ObtainedPoints));
                OnPropertyChanged(nameof(IsGraded));
                OnScoreChangedAction?.Invoke();
            }
        }

        private decimal? _obtainedPoints;
        public decimal? ObtainedPoints
        {
            get => _obtainedPoints;
            set
            {
                if (_obtainedPoints == value) return;
                _obtainedPoints = value;
                _scoreInputText = value?.ToString("0.##") ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScoreInputText));
                OnPropertyChanged(nameof(IsGraded));
                OnScoreChangedAction?.Invoke();
            }
        }

        public bool IsGraded => ObtainedPoints.HasValue;

        // ── AI grading reasoning ──────────────────────────────────────────
        private string _gradedBy;
        public string GradedBy
        {
            get => _gradedBy;
            set
            {
                if (_gradedBy == value) return;
                _gradedBy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAiSuggested));
            }
        }

        /// <summary>True if the current score/justification came from AI and the
        /// instructor hasn't overridden it yet (GradedBy stays "ai" until they Save).</summary>
        public bool IsAiSuggested => GradedBy == "ai";

        private string _aiOverallFeedback;
        public string AiOverallFeedback
        {
            get => _aiOverallFeedback;
            set
            {
                if (_aiOverallFeedback == value) return;
                _aiOverallFeedback = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<AiCriterionBreakdownVM> AiCriteriaBreakdown { get; set; } = new();

        public bool HasAiBreakdown => AiCriteriaBreakdown.Count > 0;

        public void NotifyBreakdownChanged() => OnPropertyChanged(nameof(HasAiBreakdown));

        /// <summary>True if AI grading was attempted for this answer and failed
        /// after all retries — distinct from "AI hasn't gotten to this yet."</summary>
        private bool _aiGradingFailed;
        public bool AiGradingFailed
        {
            get => _aiGradingFailed;
            set
            {
                if (_aiGradingFailed == value) return;
                _aiGradingFailed = value;
                OnPropertyChanged();
            }
        }

        private string _aiGradingError;
        public string AiGradingError
        {
            get => _aiGradingError;
            set
            {
                if (_aiGradingError == value) return;
                _aiGradingError = value;
                OnPropertyChanged();
            }
        }

        private bool _isAiRetrying;
        public bool IsAiRetrying
        {
            get => _isAiRetrying;
            set
            {
                if (_isAiRetrying == value) return;
                _isAiRetrying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotAiRetrying));
            }
        }

        public bool IsNotAiRetrying => !_isAiRetrying;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // One criterion's AI-assigned tier/score/justification, for display under a question.
    public class AiCriterionBreakdownVM
    {
        public string CriteriaName { get; set; } = string.Empty;
        public string MatchedTier { get; set; } = string.Empty;
        public decimal AwardedScore { get; set; }
        public string Justification { get; set; } = string.Empty;
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
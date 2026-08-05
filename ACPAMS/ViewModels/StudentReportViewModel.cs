using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ACPAMS.ViewModels
{
    public class StudentReportViewModel : INotifyPropertyChanged
    {
        public Guid AttemptId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string ExamName { get; set; } = string.Empty;
        public string ExamCode { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public double TotalScore { get; set; }
        public double MaxPossibleScore { get; set; }
        public double TotalScorePercent { get; set; }
        public double CheatingScore { get; set; }
        public string RiskLevel { get; set; } = "low";
        public int TotalFlags { get; set; }
        public string Status { get; set; } = "in_progress";

        public List<FlagEventDto> FlagHistory { get; set; } = new();
        public List<QuestionAnswerDto> QuestionAnswers { get; set; } = new();
        public List<CheatingScoreSnapshotDto> CheatingScoreHistory { get; set; } = new();

        // ── Performance assessment (Strengths/Weaknesses/Summary) ────────
        // Gates the "Generate Assessment" button — should only run once
        // grading is actually finalized, not on still-pending AI-suggested scores.
        public bool IsFinalized { get; set; }
        public bool HasAssessment { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public string AssessmentSummary { get; set; } = string.Empty;
        public DateTime? AssessmentGeneratedAt { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FlagEventDto
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "low";
        public string Action { get; set; } = string.Empty;
    }

    public class QuestionAnswerDto
    {
        public int Number { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string StudentAnswer { get; set; } = string.Empty;
        public double? ObtainedPoints { get; set; }
        public double MaxPoints { get; set; }
        public string Status { get; set; } = "pending";
    }

    public class CheatingScoreSnapshotDto
    {
        public DateTime Timestamp { get; set; }
        public double Score { get; set; }
    }
}
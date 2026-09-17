using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("student_answers")]
    public class StudentAnswer
    {
        [Key]
        [Column("answer_id")]
        public Guid AnswerId { get; set; } = Guid.NewGuid();

        [Column("attempt_id")]
        public Guid? AttemptId { get; set; }

        [Column("question_id")]
        public Guid? QuestionId { get; set; }

        [Column("answer_text")]
        public string? AnswerText { get; set; }

        [Column("source_code")]
        public string? SourceCode { get; set; }

        [MaxLength(30)]
        [Column("language_used")]
        public string? LanguageUsed { get; set; }

        [MaxLength(20)]
        [Column("execution_status")]
        public string ExecutionStatus { get; set; } = "not_run";
        // Values: "not_run" | "passed" | "failed" | "runtime_error"

        [Column("obtained_points")]
        public decimal? ObtainedPoints { get; set; }

        [MaxLength(50)]
        [Column("graded_by")]
        public string? GradedBy { get; set; }

        [Column("graded_at")]
        public DateTime? GradedAt { get; set; }

        [Column("is_manual_grade")]
        public bool IsManualGrade { get; set; } = false;

        // Navigation
        [ForeignKey("AttemptId")]
        public StudentExamAttempt? Attempt { get; set; }
        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }
        public ICollection<AiEvaluation> AiEvaluations { get; set; } = new List<AiEvaluation>();
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("questions")]
    public class Question
    {
        [Key]
        [Column("question_id")]
        public Guid QuestionId { get; set; } = Guid.NewGuid();

        [Column("exam_id")]
        public Guid ExamId { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("question_type")]
        public string QuestionType { get; set; } = string.Empty;
        // Values: "multiple_choice" | "true_false" | "identification"
        //         "enumeration" | "essay" | "coding"

        [Required]
        [Column("text")]
        public string Text { get; set; } = string.Empty;

        [Column("correct_answer")]
        public string? CorrectAnswer { get; set; }

        [Column("points")]
        public decimal? Points { get; set; }

        [Column("requires_rubric")]
        public bool RequiresRubric { get; set; } = false;

        [MaxLength(20)]
        [Column("grading_method")]
        public string GradingMethod { get; set; } = "objective";
        // Values: "objective" | "rubric_ai" | "manual" | "hybrid"

        [MaxLength(30)]
        [Column("programming_language")]
        public string? ProgrammingLanguage { get; set; }

        [Column("starter_code")]
        public string? StarterCode { get; set; }

        [Column("expected_output")]
        public string? ExpectedOutput { get; set; }

        [Column("order_index")]
        public int? OrderIndex { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }
        public Rubric? Rubric { get; set; }
        public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
   





        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }
}

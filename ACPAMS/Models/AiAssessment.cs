using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("ai_assessments")]
    public class AiAssessment
    {
        [Key]
        [Column("assessment_id")]
        public Guid AssessmentId { get; set; } = Guid.NewGuid();

        [MaxLength(50)]
        [Column("student_id")]
        public string? StudentId { get; set; }

        [Column("exam_id")]
        public Guid? ExamId { get; set; }

        [Column("generated_at")]
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        [Column("strengths")]
        public string? Strengths { get; set; }

        [Column("weaknesses")]
        public string? Weaknesses { get; set; }

        [Column("ai_summary")]
        public string? AiSummary { get; set; }

        [Column("raw_ai_response")]
        public string? RawAiResponse { get; set; }

        [Column("model_id")]
        public Guid? ModelId { get; set; }

        // Navigation
        [ForeignKey("StudentId")]
        public Student? Student { get; set; }
        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }
        [ForeignKey("ModelId")]
        public AiModel? Model { get; set; }
    }
}
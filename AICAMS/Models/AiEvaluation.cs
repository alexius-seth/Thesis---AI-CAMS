using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("ai_evaluations")]
    public class AiEvaluation
    {
        [Key]
        [Column("evaluation_id")]
        public Guid EvaluationId { get; set; } = Guid.NewGuid();

        [Column("answer_id")]
        public Guid? AnswerId { get; set; }

        [Column("model_id")]
        public Guid? ModelId { get; set; }

        [Column("rubric_used_id")]
        public Guid? RubricUsedId { get; set; }

        [Column("evaluated_at")]
        public DateTime EvaluatedAt { get; set; } = DateTime.Now;

        [Column("score")]
        public decimal? Score { get; set; }

        [Column("confidence_score")]
        public decimal? ConfidenceScore { get; set; }

        [Column("feedback")]
        public string? Feedback { get; set; }

        [Column("justification")]
        public string? Justification { get; set; }

        [Column("ai_response_raw")]
        public string? AiResponseRaw { get; set; }

        // Navigation
        [ForeignKey("AnswerId")]
        public StudentAnswer? Answer { get; set; }
        [ForeignKey("ModelId")]
        public AiModel? Model { get; set; }
        [ForeignKey("RubricUsedId")]
        public Rubric? Rubric { get; set; }
        public ICollection<AiRubricEvaluation> RubricEvaluations { get; set; } = new List<AiRubricEvaluation>();
    }
}
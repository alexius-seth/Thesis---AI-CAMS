using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("ai_rubric_evaluations")]
    public class AiRubricEvaluation
    {
        [Key]
        [Column("rubric_eval_id")]
        public Guid RubricEvalId { get; set; } = Guid.NewGuid();

        [Column("evaluation_id")]
        public Guid? EvaluationId { get; set; }

        [Column("criteria_id")]
        public Guid? CriteriaId { get; set; }

        [Column("level_id")]
        public Guid? LevelId { get; set; }

        [Column("assigned_score")]
        public decimal? AssignedScore { get; set; }

        [Column("feedback")]
        public string? Feedback { get; set; }

        [Column("confidence_score")]
        public decimal? ConfidenceScore { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("EvaluationId")]
        public AiEvaluation? Evaluation { get; set; }
        [ForeignKey("CriteriaId")]
        public RubricCriteria? Criteria { get; set; }
        [ForeignKey("LevelId")]
        public RubricLevel? Level { get; set; }
    }
}
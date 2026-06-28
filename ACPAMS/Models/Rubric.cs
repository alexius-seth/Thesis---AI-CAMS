using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("rubrics")]
    public class Rubric
    {
        [Key]
        [Column("rubric_id")]
        public Guid RubricId { get; set; } = Guid.NewGuid();

        [Column("question_id")]
        public Guid? QuestionId { get; set; }

        [MaxLength(150)]
        [Column("title")]
        public string? Title { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("total_weight")]
        public decimal? TotalWeight { get; set; }

        [Column("ai_instructions")]
        public string? AiInstructions { get; set; }

        [MaxLength(50)]
        [Column("created_by")]
        public string? CreatedBy { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }
        [ForeignKey("CreatedBy")]
        public Instructor? Instructor { get; set; }
        public ICollection<RubricCriteria> Criteria { get; set; } = new List<RubricCriteria>();
    }
}
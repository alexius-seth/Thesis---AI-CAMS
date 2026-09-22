using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("rubric_levels")]
    public class RubricLevel
    {
        [Key]
        [Column("level_id")]
        public Guid LevelId { get; set; } = Guid.NewGuid();

        [Column("criteria_id")]
        public Guid? CriteriaId { get; set; }

        [MaxLength(50)]
        [Column("level_name")]
        public string? LevelName { get; set; }

        [Column("score_percentage")]
        public decimal? ScorePercentage { get; set; }

        [Column("descriptor")]
        public string? Descriptor { get; set; }

        [Column("order_index")]
        public int? OrderIndex { get; set; }

        // Navigation
        [ForeignKey("CriteriaId")]
        public RubricCriteria? Criteria { get; set; }
    }
}

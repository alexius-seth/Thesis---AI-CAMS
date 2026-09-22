using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("rubric_criteria")]
    public class RubricCriteria
    {
        [Key]
        [Column("criteria_id")]
        public Guid CriteriaId { get; set; } = Guid.NewGuid();

        [Column("rubric_id")]
        public Guid? RubricId { get; set; }

        [MaxLength(100)]
        [Column("criteria_name")]
        public string? CriteriaName { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("max_score")]
        public decimal? MaxScore { get; set; }

        [Column("weight_percentage")]
        public decimal? WeightPercentage { get; set; }

        [Column("order_index")]
        public int? OrderIndex { get; set; }

        // Navigation
        [ForeignKey("RubricId")]
        public Rubric? Rubric { get; set; }
        public ICollection<RubricLevel> Levels { get; set; } = new List<RubricLevel>();
    }
}
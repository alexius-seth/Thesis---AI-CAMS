using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACPAMS.Models
{
    [Table("question_options")]
    public class QuestionOption
    {
        [Key]
        [Column("option_id")]
        public Guid OptionId { get; set; } = Guid.NewGuid();

        [Column("question_id")]
        public Guid QuestionId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("option_text")]
        public string OptionText { get; set; } = string.Empty;

        [Column("order_index")]
        public int? OrderIndex { get; set; }

        // Navigation
        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }
    }
}
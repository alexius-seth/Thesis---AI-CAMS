using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("ai_models")]
    public class AiModel
    {
        [Key]
        [Column("model_id")]
        public Guid ModelId { get; set; } = Guid.NewGuid();

        [MaxLength(100)]
        [Column("model_name")]
        public string? ModelName { get; set; }

        [MaxLength(100)]
        [Column("provider")]
        public string? Provider { get; set; }

        [MaxLength(50)]
        [Column("model_version")]
        public string? ModelVersion { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

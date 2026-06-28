using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [Column("log_id")]
        public Guid LogId { get; set; } = Guid.NewGuid();

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [MaxLength(100)]
        [Column("action")]
        public string? Action { get; set; }

        [MaxLength(100)]
        [Column("entity_type")]
        public string? EntityType { get; set; }

        [MaxLength(100)]
        [Column("entity_id")]
        public string? EntityId { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [MaxLength(45)]
        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}

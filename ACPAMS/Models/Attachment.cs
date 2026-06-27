using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("attachments")]
    public class Attachment
    {
        [Key]
        [Column("attachment_id")]
        public Guid AttachmentId { get; set; } = Guid.NewGuid();

        [MaxLength(20)]
        [Column("entity_type")]
        public string? EntityType { get; set; }
        // Values: "question" | "answer"

        [Column("entity_id")]
        public Guid? EntityId { get; set; }

        [MaxLength(255)]
        [Column("file_path")]
        public string? FilePath { get; set; }

        [MaxLength(100)]
        [Column("file_type")]
        public string? FileType { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [Column("uploaded_by")]
        public Guid? UploadedBy { get; set; }

        // Navigation
        [ForeignKey("UploadedBy")]
        public User? User { get; set; }
    }
}

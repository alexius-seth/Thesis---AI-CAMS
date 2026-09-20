using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACPAMS.Models
{
    [Table("notifications")]
    public class AppNotification
    {
        [Key]
        [Column("notification_id")]
        public Guid NotificationId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("recipient_user_id")]
        public Guid RecipientUserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("category")]
        public string Category { get; set; } = "System"; // Integrity, Submission, AiGrading, System

        [Required]
        [MaxLength(20)]
        [Column("severity")]
        public string Severity { get; set; } = "Info"; // Critical, Warning, Success, Info

        [Required]
        [MaxLength(200)]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        [Column("target_route")]
        public string? TargetRoute { get; set; } // Monitoring, StudentReport, Grading

        [MaxLength(100)]
        [Column("target_entity_id")]
        public string? TargetEntityId { get; set; }
    }
}


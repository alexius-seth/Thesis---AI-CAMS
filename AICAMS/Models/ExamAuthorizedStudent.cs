using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACPAMS.Models
{
    [Table("exam_authorized_students")]
    public class ExamAuthorizedStudent
    {
        [Key]
        [Column("authorization_id")]
        public Guid AuthorizationId { get; set; } = Guid.NewGuid();

        [Column("exam_id")]
        public Guid ExamId { get; set; }

        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("student_id")]
        public string StudentId { get; set; } = string.Empty;

        [ForeignKey("StudentId")]
        public Student? Student { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("status")]
        public string Status { get; set; } = "PendingApproval"; // "PreApproved", "PendingApproval", "Approved", "Denied"

        [Column("requested_at")]
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        [Column("valid_until")]
        public DateTime? ValidUntil { get; set; }

        [MaxLength(50)]
        [Column("approved_by_instructor_id")]
        public string? ApprovedByInstructorId { get; set; }
    }
}


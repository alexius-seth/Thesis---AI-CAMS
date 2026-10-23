using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("students")]
    public class Student
    {
        [Key]
        [MaxLength(50)]
        [Column("student_id")]
        public string StudentId { get; set; } = string.Empty;
        // Format: 12-3456-789 (UL Student ID)

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(5)]
        [Column("middle_initial")]
        public string? MiddleInitial { get; set; }

        [MaxLength(100)]
        [Column("program")]
        public string? Program { get; set; }

        [MaxLength(20)]
        [Column("year_level")]
        public string? YearLevel { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("must_change_password")]
        public bool MustChangePassword { get; set; } = false;

        [NotMapped]
        public string Id => StudentId;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [Column("avatar_image")]
        public byte[]? AvatarImage { get; set; }

        // Navigation
        [ForeignKey("UserId")]
        public User? User { get; set; }
        public ICollection<StudentExamAttempt> ExamAttempts { get; set; } = new List<StudentExamAttempt>();
        public ICollection<AiAssessment> AiAssessments { get; set; } = new List<AiAssessment>();
        public ICollection<ExamAuthorizedStudent> ExamAuthorizations { get; set; } = new List<ExamAuthorizedStudent>();
    }
}

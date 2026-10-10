using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("student_exam_attempts")]
    public class StudentExamAttempt
    {
        [Key]
        [Column("attempt_id")]
        public Guid AttemptId { get; set; } = Guid.NewGuid();

        [MaxLength(50)]
        [Column("student_id")]
        public string? StudentId { get; set; }

        [Column("exam_id")]
        public Guid? ExamId { get; set; }

        [MaxLength(100)]
        [Column("exam_code_used")]
        public string? ExamCodeUsed { get; set; }

        [Column("started_at")]
        public DateTime? StartedAt { get; set; }

        [Column("submitted_at")]
        public DateTime? SubmittedAt { get; set; }

        [MaxLength(20)]
        [Column("status")]
        public string? Status { get; set; }
        // Values: "in_progress" | "submitted" | "abandoned" | "graded"

        [Column("total_score")]
        public decimal? TotalScore { get; set; }

        [Column("extra_time_minutes")]
        public int? ExtraTimeMinutes { get; set; } = 0;

        [MaxLength(45)]
        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [MaxLength(255)]
        [Column("device_info")]
        public string? DeviceInfo { get; set; }

        [NotMapped]
        public string? DeviceName
        {
            get => DeviceInfo;
            set => DeviceInfo = value;
        }

        [NotMapped]
        public string? HostName
        {
            get => DeviceInfo;
            set => DeviceInfo = value;
        }

        // Navigation
        [ForeignKey("StudentId")]
        public Student? Student { get; set; }
        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }
        public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
    }
}

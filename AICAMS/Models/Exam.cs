using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("exams")]
    public class Exam
    {
        [Key]
        [Column("exam_id")]
        public Guid ExamId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        [Column("instructor_id")]
        public string InstructorId { get; set; } = string.Empty;

        [MaxLength(20)]
        [Column("course_code")]
        public string? CourseCode { get; set; }

        [MaxLength(100)]
        [Column("exam_code")]
        public string? ExamCode { get; set; }

        [MaxLength(200)]
        [Column("exam_name")]
        public string? ExamName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("duration_minutes")]
        public int? DurationMinutes { get; set; }

        [Column("total_points")]
        public decimal? TotalPoints { get; set; }

        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "draft";
        // Values: "draft" | "active" | "archived"

        [NotMapped]
        public bool IsArchived
        {
            get => Status == "archived";
            set => Status = value ? "archived" : "active";
        }

        [Column("instructions")]
        public string? Instructions { get; set; }

        [Column("randomize_questions")]
        public bool RandomizeQuestions { get; set; } = false;

        [MaxLength(20)]
        [Column("academic_year")]
        public string? AcademicYear { get; set; }

        [MaxLength(20)]
        [Column("semester")]
        public string? Semester { get; set; }

        [MaxLength(50)]
        [Column("section")]
        public string? Section { get; set; }

        [MaxLength(50)]
        [Column("class_schedule")]
        public string? ClassSchedule { get; set; }

        [Column("requires_approval")]
        public bool RequiresApproval { get; set; } = false;

        [Column("scheduled_start_time")]
        public DateTime? ScheduledStartTime { get; set; }

        [Column("scheduled_end_time")]
        public DateTime? ScheduledEndTime { get; set; }

        [Column("activated_at")]
        public DateTime? ActivatedAt { get; set; }

        [MaxLength(30)]
        [Column("auto_archive_rule")]
        public string AutoArchiveRule { get; set; } = "1HourAfter";

        [Column("auto_archive_buffer_minutes")]
        public int AutoArchiveBufferMinutes { get; set; } = 60;

        // Navigation
        [ForeignKey("InstructorId")]
        public Instructor? Instructor { get; set; }
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<StudentExamAttempt> Attempts { get; set; } = new List<StudentExamAttempt>();
        public ICollection<ExamAuthorizedStudent> AuthorizedStudents { get; set; } = new List<ExamAuthorizedStudent>();
    }
}

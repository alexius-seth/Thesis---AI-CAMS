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

        [MaxLength(200)]
        [Column("title")]
        public string? Title { get; set; }

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

        [Column("instructions")]
        public string? Instructions { get; set; }

        [Column("randomize_questions")]
        public bool RandomizeQuestions { get; set; } = false;

        // Navigation
        [ForeignKey("InstructorId")]
        public Instructor? Instructor { get; set; }
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<StudentExamAttempt> Attempts { get; set; } = new List<StudentExamAttempt>();
    }
}
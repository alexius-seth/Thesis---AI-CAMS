using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("instructors")]
    public class Instructor
    {
        [Key]
        [MaxLength(50)]
        [Column("instructor_id")]
        public string InstructorId { get; set; } = string.Empty;

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
        [Column("department")]
        public string? Department { get; set; } = "Department of Computer Studies";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("avatar_image")]
        public byte[]? AvatarImage { get; set; }

        // Navigation
        [ForeignKey("UserId")]
        public User? User { get; set; }
        public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }
}
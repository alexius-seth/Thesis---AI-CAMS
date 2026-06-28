using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public Guid UserId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(20)]
        [Column("role")]
        public string Role { get; set; } = string.Empty;
        // Values: "student" | "instructor" | "admin"

        [Required]
        [MaxLength(100)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navigation
        public Student? Student { get; set; }
        public Instructor? Instructor { get; set; }
    }
}
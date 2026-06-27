using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("exam_attempt_logs")]
    public class ExamAttemptLog
    {
        [Key]
        [Column("log_id")]
        public Guid LogId { get; set; } = Guid.NewGuid();

        [Column("attempt_id")]
        public Guid? AttemptId { get; set; }

        [MaxLength(100)]
        [Column("action")]
        public string? Action { get; set; }

        [Column("action_time")]
        public DateTime ActionTime { get; set; } = DateTime.Now;

        [Column("details")]
        public string? Details { get; set; }

        [MaxLength(45)]
        [Column("ip_address")]
        public string? IpAddress { get; set; }

        // Navigation
        [ForeignKey("AttemptId")]
        public StudentExamAttempt? Attempt { get; set; }
    }
}

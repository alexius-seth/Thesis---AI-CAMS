using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACPAMS.Models
{
    [Table("ai_jobs")]
    public class AiJob
    {
        [Key]
        [Column("job_id")]
        public Guid JobId { get; set; } = Guid.NewGuid();

        [Column("answer_id")]
        public Guid? AnswerId { get; set; }

        [Column("model_id")]
        public Guid? ModelId { get; set; }

        [MaxLength(20)]
        [Column("status")]
        public string? Status { get; set; }
        // Values: "queued" | "processing" | "completed" | "failed"

        [Column("priority")]
        public int? Priority { get; set; }

        [Column("attempt_count")]
        public int AttemptCount { get; set; } = 0;

        [Column("last_error")]
        public string? LastError { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("started_at")]
        public DateTime? StartedAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        // Navigation
        [ForeignKey("AnswerId")]
        public StudentAnswer? Answer { get; set; }
        [ForeignKey("ModelId")]
        public AiModel? Model { get; set; }
    }
}
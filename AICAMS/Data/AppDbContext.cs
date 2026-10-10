using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ACPAMS.Models;

namespace ACPAMS.Data
{
    /// <summary>
    /// ACPAMS Database Context
    /// Place this file in: ACPAMS/Data/AppDbContext.cs
    ///
    /// Setup steps:
    /// 1. Install NuGet packages:
    ///    - Microsoft.EntityFrameworkCore
    ///    - Microsoft.EntityFrameworkCore.SqlServer
    ///    - Microsoft.EntityFrameworkCore.Tools
    ///    - Microsoft.EntityFrameworkCore.Design
    ///
    /// 2. Run in terminal:
    ///    dotnet ef migrations add InitialCreate
    ///    dotnet ef database update
    ///
    /// 3. Teammates run after git pull:
    ///    dotnet ef database update
    /// </summary>
    public class AppDbContext : DbContext
    {
        // ── Tables ──────────────────────────────────────────
        public DbSet<User> Users { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Rubric> Rubrics { get; set; }
        public DbSet<RubricCriteria> RubricCriteria { get; set; }
        public DbSet<RubricLevel> RubricLevels { get; set; }
        public DbSet<StudentExamAttempt> StudentExamAttempts { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }
        public DbSet<AiModel> AiModels { get; set; }
        public DbSet<AiEvaluation> AiEvaluations { get; set; }
        public DbSet<AiRubricEvaluation> AiRubricEvaluations { get; set; }
        public DbSet<AiAssessment> AiAssessments { get; set; }
        public DbSet<AiJob> AiJobs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<AppNotification> Notifications { get; set; }

        // ── Connection ──────────────────────────────────────
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string? connectionString = App.Configuration?.GetConnectionString("DefaultConnection");

                if (!string.IsNullOrEmpty(connectionString))
                {
                    optionsBuilder.UseSqlServer(connectionString);
                }
                else
                {
                    optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=aicamsdb;Trusted_Connection=True;TrustServerCertificate=True;");
                }
            }
        }

        // ── Model Configuration ─────────────────────────────
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── User ────────────────────────────────────────
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasDefaultValueSql("NEWID()");
                entity.HasIndex(e => e.Username).IsUnique(); // username must be unique
                entity.Property(e => e.Role)
                      .HasConversion<string>()
                      .HasMaxLength(20);
            });

            // ── Student ─────────────────────────────────────
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(e => e.StudentId);
                entity.HasOne(e => e.User)
                      .WithOne(u => u.Student)
                      .HasForeignKey<Student>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Instructor ──────────────────────────────────
            modelBuilder.Entity<Instructor>(entity =>
            {
                entity.HasKey(e => e.InstructorId);
                entity.HasOne(e => e.User)
                      .WithOne(u => u.Instructor)
                      .HasForeignKey<Instructor>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Exam ────────────────────────────────────────
            modelBuilder.Entity<Exam>(entity =>
            {
                entity.HasKey(e => e.ExamId);
                entity.Property(e => e.ExamId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.TotalPoints).HasPrecision(10, 2);
                entity.Property(e => e.RandomizeQuestions).HasDefaultValue(false);
                entity.HasIndex(e => e.ExamCode).IsUnique();
                entity.HasOne(e => e.Instructor)
                      .WithMany(i => i.Exams)
                      .HasForeignKey(e => e.InstructorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Question ────────────────────────────────────
            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasKey(e => e.QuestionId);
                entity.Property(e => e.QuestionId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.Points).HasPrecision(10, 2);
                entity.HasKey(e => e.QuestionId);
                entity.Property(e => e.QuestionId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Exam)
                      .WithMany(ex => ex.Questions)
                      .HasForeignKey(e => e.ExamId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Rubric ──────────────────────────────────────
            modelBuilder.Entity<Rubric>(entity =>
            {
                entity.HasKey(e => e.RubricId);
                entity.Property(e => e.RubricId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.TotalWeight).HasPrecision(10, 2);
                entity.HasKey(e => e.RubricId);
                entity.Property(e => e.RubricId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Question)
                      .WithOne(q => q.Rubric)
                      .HasForeignKey<Rubric>(e => e.QuestionId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Instructor)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── RubricCriteria ──────────────────────────────
            modelBuilder.Entity<RubricCriteria>(entity =>
            {
                entity.HasKey(e => e.CriteriaId);
                entity.Property(e => e.CriteriaId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.MaxScore).HasPrecision(10, 2);
                entity.Property(e => e.WeightPercentage).HasPrecision(10, 2);
                entity.HasKey(e => e.CriteriaId);
                entity.Property(e => e.CriteriaId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Rubric)
                      .WithMany(r => r.Criteria)
                      .HasForeignKey(e => e.RubricId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── RubricLevel ─────────────────────────────────
            modelBuilder.Entity<RubricLevel>(entity =>
            {
                entity.HasKey(e => e.LevelId);
                entity.Property(e => e.LevelId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.ScorePercentage).HasPrecision(10, 2);
                entity.HasKey(e => e.LevelId);
                entity.Property(e => e.LevelId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Criteria)
                      .WithMany(c => c.Levels)
                      .HasForeignKey(e => e.CriteriaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── StudentExamAttempt ──────────────────────────
            modelBuilder.Entity<StudentExamAttempt>(entity =>
            {
                entity.HasKey(e => e.AttemptId);
                entity.Property(e => e.AttemptId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.TotalScore).HasPrecision(10, 2);
                entity.Property(e => e.ExtraTimeMinutes).HasDefaultValue(0);
                entity.HasOne(e => e.Student)
                      .WithMany(s => s.ExamAttempts)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Exam)
                      .WithMany(ex => ex.Attempts)
                      .HasForeignKey(e => e.ExamId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── StudentAnswer ───────────────────────────────
            modelBuilder.Entity<StudentAnswer>(entity =>
            {
                entity.HasKey(e => e.AnswerId);
                entity.Property(e => e.AnswerId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.ObtainedPoints).HasPrecision(10, 2); 
                entity.HasOne(e => e.Attempt)
                      .WithMany(a => a.Answers)
                      .HasForeignKey(e => e.AttemptId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Question)
                      .WithMany(q => q.StudentAnswers)
                      .HasForeignKey(e => e.QuestionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── AiModel ─────────────────────────────────────
            modelBuilder.Entity<AiModel>(entity =>
            {
                entity.HasKey(e => e.ModelId);
                entity.Property(e => e.ModelId).HasDefaultValueSql("NEWID()");
            });

            // ── AiEvaluation ────────────────────────────────
            modelBuilder.Entity<AiEvaluation>(entity =>
            {
                entity.HasKey(e => e.EvaluationId);
                entity.Property(e => e.EvaluationId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.Score).HasPrecision(10, 2);
                entity.Property(e => e.ConfidenceScore).HasPrecision(10, 2);
                entity.HasKey(e => e.EvaluationId);
                entity.Property(e => e.EvaluationId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Answer)
                      .WithMany(a => a.AiEvaluations)
                      .HasForeignKey(e => e.AnswerId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Model)
                      .WithMany()
                      .HasForeignKey(e => e.ModelId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Rubric)
                      .WithMany()
                      .HasForeignKey(e => e.RubricUsedId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── AiRubricEvaluation ──────────────────────────
            modelBuilder.Entity<AiRubricEvaluation>(entity =>
            {
                entity.HasKey(e => e.RubricEvalId);
                entity.Property(e => e.RubricEvalId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.AssignedScore).HasPrecision(10, 2);
                entity.Property(e => e.ConfidenceScore).HasPrecision(10, 2);
                entity.HasKey(e => e.RubricEvalId);
                entity.Property(e => e.RubricEvalId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Evaluation)
                      .WithMany(e => e.RubricEvaluations)
                      .HasForeignKey(e => e.EvaluationId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Criteria)
                      .WithMany()
                      .HasForeignKey(e => e.CriteriaId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Level)
                      .WithMany()
                      .HasForeignKey(e => e.LevelId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── AiAssessment ────────────────────────────────
            modelBuilder.Entity<AiAssessment>(entity =>
            {
                entity.HasKey(e => e.AssessmentId);
                entity.Property(e => e.AssessmentId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Student)
                      .WithMany(s => s.AiAssessments)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Exam)
                      .WithMany()
                      .HasForeignKey(e => e.ExamId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Model)
                      .WithMany()
                      .HasForeignKey(e => e.ModelId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── AiJob ───────────────────────────────────────
            modelBuilder.Entity<AiJob>(entity =>
            {
                entity.HasKey(e => e.JobId);
                entity.Property(e => e.JobId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Answer)
                      .WithMany()
                      .HasForeignKey(e => e.AnswerId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Model)
                      .WithMany()
                      .HasForeignKey(e => e.ModelId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── AuditLog ────────────────────────────────────
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.LogId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.IsDismissed).HasDefaultValue(false);
                entity.Property(e => e.DismissalReason).HasMaxLength(255);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Attachment ──────────────────────────────────
            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.HasKey(e => e.AttachmentId);
                entity.Property(e => e.AttachmentId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UploadedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });
           
            // ── Question Option ──────────────────────────────────
            modelBuilder.Entity<QuestionOption>(entity =>
            {
                entity.HasKey(e => e.OptionId);
                entity.Property(e => e.OptionId).HasDefaultValueSql("NEWID()");
                entity.HasOne(e => e.Question)
                      .WithMany(q => q.Options)
                      .HasForeignKey(e => e.QuestionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
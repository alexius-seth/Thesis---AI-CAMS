using Microsoft.EntityFrameworkCore;
using ACPAMS.Models;

namespace ACPAMS
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<Rubric> Rubrics { get; set; }
        public DbSet<RubricCriteria> RubricCriteria { get; set; }
        public DbSet<RubricLevel> RubricLevels { get; set; }
        public DbSet<StudentExamAttempt> StudentExamAttempts { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }
        public DbSet<AiEvaluation> AiEvaluations { get; set; }
        public DbSet<AiRubricEvaluation> AiRubricEvaluations { get; set; }
        public DbSet<AiAssessment> AiAssessments { get; set; }
        public DbSet<AiModel> AiModels { get; set; }
        public DbSet<AiJob> AiJobs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ExamAttemptLog> ExamAttemptLogs { get; set; }
        public DbSet<Attachment> Attachments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Use SQL Server LocalDB (built-in to Visual Studio)
            var connectionString = "Server=(localdb)\\mssqllocaldb;Database=ACPAMS_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

            optionsBuilder.UseSqlServer(connectionString);
        }
    }
}
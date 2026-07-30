using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace ACPAMS.Services
{
    /// <summary>
    /// Owns saving a rubric (built in AIRubricModal) into the database, and
    /// loading it back out in the shape GeminiRubricGradingService needs.
    ///
    /// This did not exist before — AIRubricModal.GetRubricData() was being
    /// collected into CreateExamPage's _rubricData dictionary, but nothing
    /// ever turned that into actual Rubric/RubricCriteria/RubricLevel rows,
    /// and the attached reference PDF (AIRubricModal.FilePath) was being
    /// discarded entirely. This service is what's called from ExamService
    /// after each Question is saved, to fix both of those gaps.
    /// </summary>
    public class RubricService
    {
        // Attachments get copied here so they survive even if the instructor's
        // original file (e.g. from Downloads) is later moved, renamed, or deleted.
        private static readonly string AttachmentStorageRoot =
            Path.Combine(AppContext.BaseDirectory, "RubricAttachments");

        public async Task SaveRubricForQuestionAsync(
            Guid questionId,
            RubricData rubricData,
            string? attachmentSourcePath,
            string instructorId)
        {
            using var context = new AppDbContext();

            var rubric = new Rubric
            {
                RubricId = Guid.NewGuid(),
                QuestionId = questionId,
                Title = rubricData.Description,
                Description = rubricData.Description,
                AiInstructions = rubricData.AiInstructions,
                TotalWeight = rubricData.TotalWeight,
                IsActive = true,
                CreatedBy = instructorId,
                CreatedAt = DateTime.Now
            };
            context.Rubrics.Add(rubric);

            int criteriaOrder = 0;
            foreach (var criteria in rubricData.Criteria)
            {
                var criteriaEntity = new RubricCriteria
                {
                    CriteriaId = Guid.NewGuid(),
                    RubricId = rubric.RubricId,
                    CriteriaName = criteria.Name,
                    Description = criteria.Description,
                    MaxScore = (decimal)criteria.MaxScore, // RubricCriteriaData.MaxScore is double; entity wants decimal?
                    WeightPercentage = criteria.WeightPercentage,
                    OrderIndex = criteriaOrder++
                };
                context.RubricCriteria.Add(criteriaEntity);

                int levelOrder = 0;
                foreach (var level in criteria.Levels)
                {
                    context.RubricLevels.Add(new RubricLevel
                    {
                        LevelId = Guid.NewGuid(),
                        CriteriaId = criteriaEntity.CriteriaId,
                        LevelName = level.Name,
                        ScorePercentage = (decimal)level.ScorePercentage, // RubricLevelData.ScorePercentage is double; entity wants decimal?
                        Descriptor = level.Descriptor,
                        OrderIndex = levelOrder++
                    });
                }
            }

            // ── Attachment (the previously-discarded PDF) ──────────────
            if (!string.IsNullOrEmpty(attachmentSourcePath) && File.Exists(attachmentSourcePath))
            {
                Directory.CreateDirectory(AttachmentStorageRoot);

                string extension = Path.GetExtension(attachmentSourcePath);
                string storedFileName = $"{rubric.RubricId}{extension}";
                string storedPath = Path.Combine(AttachmentStorageRoot, storedFileName);

                File.Copy(attachmentSourcePath, storedPath, overwrite: true);

                context.Attachments.Add(new Attachment
                {
                    AttachmentId = Guid.NewGuid(),
                    EntityType = "Rubric",
                    EntityId = rubric.RubricId,
                    FilePath = storedPath,
                    FileType = extension.TrimStart('.').ToUpperInvariant(),
                    UploadedAt = DateTime.Now
                    // UploadedBy (Guid? → Users.UserId) intentionally left null —
                    // this method only receives the instructor's string InstructorId,
                    // not their User.UserId. Thread that through if you want this tracked.
                });
            }

            await context.SaveChangesAsync();
        }

        // ── Load a question's rubric back out, ready for GeminiRubricGradingService ──
        // Returns null if this question has no rubric saved.
        public async Task<RubricForGradingDto?> GetRubricForGradingAsync(Guid questionId)
        {
            using var context = new AppDbContext();

            var rubric = await context.Rubrics
                .Include(r => r.Criteria)
                    .ThenInclude(c => c.Levels)
                .FirstOrDefaultAsync(r => r.QuestionId == questionId);

            if (rubric == null) return null;

            var criteria = rubric.Criteria.Select(c => new RubricCriterionDto
            {
                Name = c.CriteriaName ?? string.Empty,
                MaxScore = c.MaxScore ?? 0,
                Levels = c.Levels.Select(l => new RubricTierDto
                {
                    TierName = l.LevelName ?? string.Empty,
                    ScorePercentage = l.ScorePercentage ?? 0,
                    Descriptor = l.Descriptor ?? string.Empty
                }).ToList()
            }).ToList();

            string? attachmentPath = await context.Attachments
                .Where(a => a.EntityType == "Rubric" && a.EntityId == rubric.RubricId)
                .Select(a => a.FilePath)
                .FirstOrDefaultAsync();

            return new RubricForGradingDto
            {
                Criteria = criteria,
                InstructorInstructions = rubric.AiInstructions ?? string.Empty,
                AttachmentPath = attachmentPath
            };
        }
    }

    public class RubricForGradingDto
    {
        public List<RubricCriterionDto> Criteria { get; set; } = new();
        public string InstructorInstructions { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
    }
}
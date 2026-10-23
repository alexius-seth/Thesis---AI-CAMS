using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;
using ACPAMS.Models;

namespace ACPAMS.Services
{
    public class ExamCloningService
    {
        public async Task<Exam> CloneArchivedExamAsync(
            Guid sourceExamId,
            List<Guid> selectedQuestionIds,
            string newExamCode,
            string newSection,
            string newSchedule,
            string academicYear,
            string semester)
        {
            using var context = new AppDbContext();

            var sourceExam = await context.Exams
                .AsNoTracking()
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Options)
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Rubric)
                        .ThenInclude(r => r.Criteria)
                            .ThenInclude(c => c.Levels)
                .FirstOrDefaultAsync(e => e.ExamId == sourceExamId);

            if (sourceExam == null)
            {
                throw new InvalidOperationException($"Archived exam with ID '{sourceExamId}' was not found.");
            }

            var selectedQuestions = sourceExam.Questions
                .Where(q => selectedQuestionIds.Contains(q.QuestionId))
                .OrderBy(q => q.OrderIndex ?? 0)
                .ToList();

            var newExamId = Guid.NewGuid();
            var clonedExam = new Exam
            {
                ExamId = newExamId,
                InstructorId = sourceExam.InstructorId,
                CourseCode = sourceExam.CourseCode,
                ExamCode = newExamCode,
                ExamName = $"{sourceExam.ExamName} (Cloned)",
                CreatedAt = DateTime.Now,
                DurationMinutes = sourceExam.DurationMinutes,
                Status = "draft",
                Instructions = sourceExam.Instructions,
                RandomizeQuestions = sourceExam.RandomizeQuestions,
                AcademicYear = academicYear,
                Semester = semester,
                Section = newSection,
                ClassSchedule = newSchedule,
                RequiresApproval = sourceExam.RequiresApproval,
                Questions = new List<Question>()
            };

            decimal totalPoints = 0;
            int orderIndex = 1;

            foreach (var srcQ in selectedQuestions)
            {
                var newQuestionId = Guid.NewGuid();
                var clonedQ = new Question
                {
                    QuestionId = newQuestionId,
                    ExamId = newExamId,
                    QuestionType = srcQ.QuestionType,
                    Text = srcQ.Text,
                    CorrectAnswer = srcQ.CorrectAnswer,
                    Points = srcQ.Points,
                    RequiresRubric = srcQ.RequiresRubric,
                    GradingMethod = srcQ.GradingMethod,
                    ProgrammingLanguage = srcQ.ProgrammingLanguage,
                    StarterCode = srcQ.StarterCode,
                    OrderIndex = orderIndex++,
                    CreatedAt = DateTime.Now,
                    SectionName = srcQ.SectionName,
                    Options = new List<QuestionOption>()
                };

                totalPoints += (srcQ.Points ?? 0);

                // Clone Options
                if (srcQ.Options != null && srcQ.Options.Any())
                {
                    foreach (var srcOpt in srcQ.Options.OrderBy(o => o.OrderIndex ?? 0))
                    {
                        clonedQ.Options.Add(new QuestionOption
                        {
                            OptionId = Guid.NewGuid(),
                            QuestionId = newQuestionId,
                            OptionText = srcOpt.OptionText,
                            OrderIndex = srcOpt.OrderIndex
                        });
                    }
                }

                // Clone Rubric Hierarchy
                if (srcQ.Rubric != null)
                {
                    var newRubricId = Guid.NewGuid();
                    var clonedRubric = new Rubric
                    {
                        RubricId = newRubricId,
                        QuestionId = newQuestionId,
                        Title = srcQ.Rubric.Title,
                        Description = srcQ.Rubric.Description,
                        TotalWeight = srcQ.Rubric.TotalWeight,
                        AiInstructions = srcQ.Rubric.AiInstructions,
                        CreatedBy = sourceExam.InstructorId,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        Criteria = new List<RubricCriteria>()
                    };

                    if (srcQ.Rubric.Criteria != null && srcQ.Rubric.Criteria.Any())
                    {
                        foreach (var srcCrit in srcQ.Rubric.Criteria.OrderBy(c => c.OrderIndex ?? 0))
                        {
                            var newCriteriaId = Guid.NewGuid();
                            var clonedCrit = new RubricCriteria
                            {
                                CriteriaId = newCriteriaId,
                                RubricId = newRubricId,
                                CriteriaName = srcCrit.CriteriaName,
                                Description = srcCrit.Description,
                                MaxScore = srcCrit.MaxScore,
                                WeightPercentage = srcCrit.WeightPercentage,
                                OrderIndex = srcCrit.OrderIndex,
                                Levels = new List<RubricLevel>()
                            };

                            if (srcCrit.Levels != null && srcCrit.Levels.Any())
                            {
                                foreach (var srcLvl in srcCrit.Levels.OrderBy(l => l.OrderIndex ?? 0))
                                {
                                    clonedCrit.Levels.Add(new RubricLevel
                                    {
                                        LevelId = Guid.NewGuid(),
                                        CriteriaId = newCriteriaId,
                                        LevelName = srcLvl.LevelName,
                                        ScorePercentage = srcLvl.ScorePercentage,
                                        Descriptor = srcLvl.Descriptor,
                                        OrderIndex = srcLvl.OrderIndex
                                    });
                                }
                            }

                            clonedRubric.Criteria.Add(clonedCrit);
                        }
                    }

                    clonedQ.Rubric = clonedRubric;
                }

                clonedExam.Questions.Add(clonedQ);
            }

            clonedExam.TotalPoints = totalPoints;

            context.Exams.Add(clonedExam);
            await context.SaveChangesAsync();

            return clonedExam;
        }

        public async Task<List<Exam>> GetArchivedExamsAsync(string? instructorId = null)
        {
            using var context = new AppDbContext();
            var query = context.Exams
                .AsNoTracking()
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Options)
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Rubric)
                        .ThenInclude(r => r.Criteria)
                            .ThenInclude(c => c.Levels)
                .Where(e => e.Status == "archived");

            if (!string.IsNullOrEmpty(instructorId))
            {
                query = query.Where(e => e.InstructorId == instructorId);
            }

            return await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }
    }
}


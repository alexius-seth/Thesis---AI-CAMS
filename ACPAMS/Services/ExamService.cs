using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Models;

namespace ACPAMS.Services
{
    public class ExamService
    {
        private readonly AppDbContext _context;
        private readonly string _currentInstructorId;

        public class ExamSectionData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string Instructions { get; set; }
            public bool IsActive { get; set; }
            public List<QuestionItemData> Questions { get; set; } = new List<QuestionItemData>();
        }

        public class QuestionItemData
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public int Points { get; set; }
            public string Type { get; set; }
            public int Order { get; set; }
            public string CorrectAnswer { get; set; }
            public string OptionA { get; set; }
            public string OptionB { get; set; }
            public string OptionC { get; set; }
            public string OptionD { get; set; }
            public string ExpectedAnswer { get; set; }
            public bool? IsTrue { get; set; }
            public string ProgrammingLanguage { get; set; }
            public string SectionId { get; set; }
            public string ExpectedAnswers { get; set; }
        }

        public ExamService(AppDbContext context, string instructorId)
        {
            _context = context;
            _currentInstructorId = instructorId;
        }

        public async Task<Exam> SaveExamAsync(
            string examName,
            string courseCode,
            string examCode,
            int durationMinutes,
            List<ExamSectionData> sections,
            Dictionary<string, RubricData> rubricData)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var exam = new Exam
                {
                    ExamId = Guid.NewGuid(),
                    InstructorId = _currentInstructorId,
                    Title = examName,
                    CourseCode = courseCode,
                    ExamCode = examCode,
                    ExamName = examName,
                    DurationMinutes = durationMinutes,
                    Status = "active",
                    CreatedAt = DateTime.Now
                };

                await _context.Exams.AddAsync(exam);

                int orderIndex = 0;
                foreach (var section in sections)
                {
                    foreach (var questionItem in section.Questions)
                    {
                        orderIndex++;
                        var question = new Question
                        {
                            QuestionId = Guid.NewGuid(),
                            ExamId = exam.ExamId,
                            QuestionType = MapQuestionType(questionItem.Type),
                            Text = questionItem.Text,
                            Points = questionItem.Points,
                            OrderIndex = orderIndex,
                            CreatedAt = DateTime.Now,
                            GradingMethod = DetermineGradingMethod(questionItem.Type)
                        };

                        switch (questionItem.Type)
                        {
                            case "Identification":
                                question.CorrectAnswer = questionItem.ExpectedAnswer;
                                question.RequiresRubric = false;
                                break;

                            case "Multiple Choice":
                                question.CorrectAnswer = questionItem.CorrectAnswer;
                                question.RequiresRubric = false;

                                var options = new List<QuestionOption>();
                                if (!string.IsNullOrEmpty(questionItem.OptionA))
                                    options.Add(new QuestionOption { OptionText = questionItem.OptionA, OrderIndex = 0 });
                                if (!string.IsNullOrEmpty(questionItem.OptionB))
                                    options.Add(new QuestionOption { OptionText = questionItem.OptionB, OrderIndex = 1 });
                                if (!string.IsNullOrEmpty(questionItem.OptionC))
                                    options.Add(new QuestionOption { OptionText = questionItem.OptionC, OrderIndex = 2 });
                                if (!string.IsNullOrEmpty(questionItem.OptionD))
                                    options.Add(new QuestionOption { OptionText = questionItem.OptionD, OrderIndex = 3 });

                                foreach (var opt in options)
                                {
                                    opt.QuestionId = question.QuestionId;
                                    await _context.QuestionOptions.AddAsync(opt);
                                }
                                break;

                            case "True or False":
                                question.CorrectAnswer = questionItem.IsTrue == true ? "True" : "False";
                                question.RequiresRubric = false;
                                break;

                            case "Enumeration":
                                question.CorrectAnswer = questionItem.ExpectedAnswers;
                                question.RequiresRubric = false;
                                break;

                            case "Essay":
                                question.RequiresRubric = true;
                                question.GradingMethod = "rubric_ai";
                                break;

                            case "Coding":
                                question.ProgrammingLanguage = questionItem.ProgrammingLanguage ?? "Java";
                                question.StarterCode = "// Write your code here";
                                question.RequiresRubric = true;
                                question.GradingMethod = "rubric_ai";
                                break;
                        }

                        await _context.Questions.AddAsync(question);

                        if (rubricData != null && rubricData.ContainsKey(questionItem.Id))
                        {
                            await SaveRubricAsync(question.QuestionId, rubricData[questionItem.Id]);
                        }
                    }
                }

                exam.TotalPoints = sections.SelectMany(s => s.Questions).Sum(q => q.Points);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return exam;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to save exam: {ex.Message}", ex);
            }
        }

        private async Task SaveRubricAsync(Guid questionId, RubricData rubricData)
        {
            var rubric = new Rubric
            {
                RubricId = Guid.NewGuid(),
                QuestionId = questionId,
                Title = $"Rubric for Question",
                Description = rubricData.Description ?? "AI Generated Rubric",
                AiInstructions = rubricData.AiInstructions,
                TotalWeight = rubricData.TotalWeight ?? 100,
                CreatedBy = _currentInstructorId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            await _context.Rubrics.AddAsync(rubric);

            int criteriaOrder = 0;
            foreach (var criteriaData in rubricData.Criteria)
            {
                criteriaOrder++;
                var criteria = new RubricCriteria
                {
                    CriteriaId = Guid.NewGuid(),
                    RubricId = rubric.RubricId,
                    CriteriaName = criteriaData.Name,
                    Description = criteriaData.Description,
                    MaxScore = criteriaData.MaxScore,
                    WeightPercentage = criteriaData.WeightPercentage ?? 100m / rubricData.Criteria.Count,
                    OrderIndex = criteriaOrder
                };

                await _context.RubricCriteria.AddAsync(criteria);

                int levelOrder = 0;
                foreach (var levelData in criteriaData.Levels)
                {
                    levelOrder++;
                    var level = new RubricLevel
                    {
                        LevelId = Guid.NewGuid(),
                        CriteriaId = criteria.CriteriaId,
                        LevelName = levelData.Name,
                        ScorePercentage = levelData.ScorePercentage,
                        Descriptor = levelData.Descriptor,
                        OrderIndex = levelOrder
                    };
                    await _context.RubricLevels.AddAsync(level);
                }
            }
        }

        private string MapQuestionType(string type)
        {
            return type switch
            {
                "Identification" => "identification",
                "Multiple Choice" => "multiple_choice",
                "True or False" => "true_false",
                "Essay" => "essay",
                "Enumeration" => "enumeration",
                "Coding" => "coding",
                _ => "essay"
            };
        }

        private string DetermineGradingMethod(string type)
        {
            return type switch
            {
                "Essay" => "rubric_ai",
                "Coding" => "rubric_ai",
                _ => "objective"
            };
        }
    }

    public class RubricData
    {
        public string Description { get; set; }
        public string AiInstructions { get; set; }
        public decimal? TotalWeight { get; set; }
        public List<RubricCriteriaData> Criteria { get; set; } = new List<RubricCriteriaData>();
    }

    public class RubricCriteriaData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal? MaxScore { get; set; }
        public decimal? WeightPercentage { get; set; }
        public List<RubricLevelData> Levels { get; set; } = new List<RubricLevelData>();
    }

    public class RubricLevelData
    {
        public string Name { get; set; }
        public decimal ScorePercentage { get; set; }
        public string Descriptor { get; set; }
    }
}
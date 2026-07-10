using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class ExamService
    {
        public ExamService() { }
        public ExamService(AppDbContext dbContext, string instructorId) { }

        public async Task<List<Question>> GetQuestionsAsync(Guid examId)
        {
            using var context = new AppDbContext();
            return await context.Questions
                .Include(q => q.Options)
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();
        }

        public async Task SaveAnswerAsync(Guid attemptId, Guid questionId,
                                          string? answerText = null,
                                          string? sourceCode = null,
                                          string? languageUsed = null)
        {
            using var context = new AppDbContext();

            var existing = await context.StudentAnswers
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId
                                       && a.QuestionId == questionId);

            if (existing != null)
            {
                existing.AnswerText = answerText;
                existing.SourceCode = sourceCode;
                existing.LanguageUsed = languageUsed;
            }
            else
            {
                context.StudentAnswers.Add(new StudentAnswer
                {
                    AnswerId = Guid.NewGuid(),
                    AttemptId = attemptId,
                    QuestionId = questionId,
                    AnswerText = answerText,
                    SourceCode = sourceCode,
                    LanguageUsed = languageUsed,
                    ExecutionStatus = "not_run"
                });
            }

            await context.SaveChangesAsync();
        }

        public async Task SubmitExamAsync(Guid attemptId)
        {
            using var context = new AppDbContext();

            var attempt = await context.StudentExamAttempts
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

            if (attempt != null)
            {
                attempt.Status = "submitted";
                attempt.SubmittedAt = DateTime.Now;
                await context.SaveChangesAsync();
            }
        }

        public async Task<StudentAnswer?> GetAnswerAsync(Guid attemptId, Guid questionId)
        { 
            using var context = new AppDbContext();
            return await context.StudentAnswers
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId
                                       && a.QuestionId == questionId);
        }

        public class ExamSectionData
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Instructions { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public List<QuestionItemData> Questions { get; set; } = new();
        }

        public class QuestionItemData
        {
            public string Id { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string CorrectAnswer { get; set; } = string.Empty;
            public string OptionA { get; set; } = string.Empty;
            public string OptionB { get; set; } = string.Empty;
            public string OptionC { get; set; } = string.Empty;
            public string OptionD { get; set; } = string.Empty;
            public string ExpectedAnswer { get; set; } = string.Empty;
            public string ExpectedAnswers { get; set; } = string.Empty;  
            public string ProgrammingLanguage { get; set; } = string.Empty;
            public string SectionId { get; set; } = string.Empty;
            public double Points { get; set; }
            public int Order { get; set; }
            public bool? IsTrue { get; set; }
        }

        public async Task<Exam> SaveExamAsync(
     string examName,
     string courseCode,
     string generatedExamCode,
     int timeLimitMinutes,
     List<ExamSectionData> sections,
     Dictionary<string, RubricData> rubricData,
     string instructorId)
        {
            using var context = new AppDbContext();

            var exam = new Exam
            {
                ExamId = Guid.NewGuid(),
                InstructorId = instructorId,
                ExamName = examName,
                Title = examName,
                CourseCode = courseCode,
                ExamCode = generatedExamCode,
                DurationMinutes = timeLimitMinutes,
                Status = "active",
                CreatedAt = DateTime.Now
            };

            context.Exams.Add(exam);

            int orderIndex = 1;
            foreach (var section in sections)
            {
                foreach (var q in section.Questions)
                {
                    var normalizedType = q.Type.ToLower() switch
                    {
                        "multiple choice" or "multiple_choice" => "multiple_choice",
                        "true or false" or "true_false" => "true_false",
                        "identification" => "identification",
                        "enumeration" => "enumeration",
                        "essay" => "essay",
                        "coding" => "coding",
                        _ => q.Type.ToLower()
                    };

                    var question = new Question
                    {
                        QuestionId = Guid.NewGuid(),
                        ExamId = exam.ExamId,
                        QuestionType = normalizedType,
                        Text = q.Text,
                        CorrectAnswer = q.CorrectAnswer,
                        ProgrammingLanguage = q.ProgrammingLanguage,
                        ExpectedOutput = q.ExpectedAnswer,
                        Points = (decimal)q.Points,
                        SectionName = sections.FirstOrDefault(s => s.Id == q.SectionId)?.Name ?? "General",
                        OrderIndex = orderIndex++,
                        GradingMethod = rubricData.ContainsKey(q.Id) ? "manual" : "objective",
                        RequiresRubric = rubricData.ContainsKey(q.Id)
                    };

                    context.Questions.Add(question);

                    if (normalizedType == "multiple_choice")
                    {
                        var options = new[]
                        {
                    new QuestionOption { QuestionId = question.QuestionId, OptionText = q.OptionA, OrderIndex = 1 },
                    new QuestionOption { QuestionId = question.QuestionId, OptionText = q.OptionB, OrderIndex = 2 },
                    new QuestionOption { QuestionId = question.QuestionId, OptionText = q.OptionC, OrderIndex = 3 },
                    new QuestionOption { QuestionId = question.QuestionId, OptionText = q.OptionD, OrderIndex = 4 },
                };
                        context.QuestionOptions.AddRange(options);
                    }
                }
            }

            await context.SaveChangesAsync();
            return exam;
        }
    }
}
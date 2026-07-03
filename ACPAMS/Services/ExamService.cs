using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class ExamService
    {
        // No shared _context field — each method creates and disposes
        // its own AppDbContext to avoid "second operation started on
        // this context" threading errors with EF Core.

        // Load all questions for an exam ordered by index
        public async Task<List<Question>> GetQuestionsAsync(Guid examId)
        {
            using var context = new AppDbContext();
            return await context.Questions
                .Include(q => q.Options)   
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();
        }

        // Save or update a student answer
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

        // Submit the exam — mark attempt as submitted
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

        // Get existing answer for a question
        public async Task<StudentAnswer?> GetAnswerAsync(Guid attemptId, Guid questionId)
        {
            using var context = new AppDbContext();
            return await context.StudentAnswers
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId
                                       && a.QuestionId == questionId);
        }
        // Add these nested classes inside the ExamService class
        public class ExamSectionData
        {
            public string Id { get; set; } = string.Empty;
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
            public string ProgrammingLanguage { get; set; } = string.Empty;
            public string SectionId { get; set; } = string.Empty;
            public List<string> ExpectedAnswers { get; set; } = new();
        }

    
        public async Task<Exam> SaveExamAsync(
            string examName,
            string courseCode,
            string generatedExamCode,
            int timeLimitMinutes,
            List<ExamSectionData> sections,
            Dictionary<string, RubricData> rubricData)
        {
            using var context = new AppDbContext();

            var exam = new Exam
            {
                ExamId = Guid.NewGuid(),
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
                    var question = new Question
                    {
                        QuestionId = Guid.NewGuid(),
                        ExamId = exam.ExamId,
                        QuestionType = q.Type,
                        Text = q.Text,
                        CorrectAnswer = q.CorrectAnswer,
                        ProgrammingLanguage = q.ProgrammingLanguage,
                        ExpectedOutput = q.ExpectedAnswer,
                        OrderIndex = orderIndex++,
                        GradingMethod = rubricData.ContainsKey(q.Id) ? "manual" : "objective",
                        RequiresRubric = rubricData.ContainsKey(q.Id)
                    };

                    context.Questions.Add(question);

                    // Add MCQ options if applicable
                    if (q.Type == "multiple_choice")
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
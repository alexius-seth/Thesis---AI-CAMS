using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ACPAMS.Services
{
    public class ExamService
    {
        public ExamService() { }
        public ExamService(AppDbContext dbContext, string instructorId) { }

        public async Task<List<Question>> GetQuestionsAsync(Guid examId, Guid? attemptId = null)
        {
            using var context = new AppDbContext();
            var examInfo = await context.Exams
                .Where(e => e.ExamId == examId)
                .Select(e => new { e.RandomizeQuestions, e.Instructions })
                .FirstOrDefaultAsync();

            var questions = await context.Questions
                .Include(q => q.Options)
                .Where(q => q.ExamId == examId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();

            if (examInfo != null && !string.IsNullOrWhiteSpace(examInfo.Instructions))
            {
                try
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(examInfo.Instructions);
                    if (map != null)
                    {
                        foreach (var q in questions)
                        {
                            if (!string.IsNullOrWhiteSpace(q.SectionName) && map.TryGetValue(q.SectionName, out var inst))
                            {
                                q.SectionInstructions = inst;
                            }
                            else if (map.TryGetValue("General", out var genInst))
                            {
                                q.SectionInstructions = genInst;
                            }
                        }
                    }
                }
                catch
                {
                    foreach (var q in questions)
                    {
                        q.SectionInstructions = examInfo.Instructions;
                    }
                }
            }

            if (examInfo != null && examInfo.RandomizeQuestions && attemptId.HasValue)
            {
                var rng = new Random(attemptId.Value.GetHashCode());
                return questions.OrderBy(_ => rng.Next()).ToList();
            }

            return questions;
        }

        public async Task SaveAnswerAsync(Guid attemptId, Guid questionId,
                                          string? answerText = null,
                                          string? sourceCode = null,
                                          string? languageUsed = null,
                                          string? executionOutput = null)
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
                if (executionOutput != null)
                    existing.ExecutionOutput = executionOutput;
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
                    ExecutionOutput = executionOutput,
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

            // Auto-grade objective questions immediately upon submit
            await new AutoCheckerService().AutoGradeAttemptAsync(attemptId);

            // Kick off AI grading for any essay/coding answers in this attempt.
            await new AiJobService().EnqueueGradingJobsForAttemptAsync(attemptId);
        }

        public async Task<StudentAnswer?> GetAnswerAsync(Guid attemptId, Guid questionId)
        {
            using var context = new AppDbContext();
            return await context.StudentAnswers
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId
                                       && a.QuestionId == questionId);
        }

        public async Task CheckAndAutoArchiveExpiredExamsAsync()
        {
            using var context = new AppDbContext();
            var service = new ExamStatusService(context);
            await service.CheckAndAutoArchiveExpiredExamsAsync();
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
            public string? StarterCode { get; set; }
            public bool? IsTrue { get; set; }
        }

        public async Task<Exam> SaveExamAsync(
            string examName,
            string courseCode,
            string generatedExamCode,
            int timeLimitMinutes,
            List<ExamSectionData> sections,
            Dictionary<string, RubricData> rubricData,
            string instructorId,
            bool randomizeQuestions = false,
            Guid? existingExamId = null,
            string? academicYear = null,
            string? semester = null,
            string? section = null,
            string? classSchedule = null,
            bool requiresApproval = false,
            DateTime? scheduledStartTime = null,
            DateTime? scheduledEndTime = null,
            string autoArchiveRule = "1HourAfter",
            int autoArchiveBufferMinutes = 60)
        {
            using var context = new AppDbContext();

            var sectionInstructionsMap = sections
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Instructions))
                .GroupBy(s => s.Name)
                .ToDictionary(g => g.Key, g => g.First().Instructions);
            string? serializedInstructions = sectionInstructionsMap.Count > 0
                ? JsonSerializer.Serialize(sectionInstructionsMap)
                : null;

            Exam exam;
            if (existingExamId.HasValue)
            {
                var existing = await context.Exams
                    .Include(e => e.Questions)
                        .ThenInclude(q => q.Options)
                    .Include(e => e.Questions)
                        .ThenInclude(q => q.Rubric)
                    .FirstOrDefaultAsync(e => e.ExamId == existingExamId.Value);

                if (existing != null)
                {
                    exam = existing;
                    exam.ExamName = examName;
                    exam.CourseCode = courseCode;
                    exam.ExamCode = generatedExamCode;
                    exam.DurationMinutes = timeLimitMinutes;
                    exam.Status = "active";
                    exam.CreatedAt = DateTime.Now;
                    exam.RandomizeQuestions = randomizeQuestions;
                    exam.Instructions = serializedInstructions;
                    exam.AcademicYear = academicYear;
                    exam.Semester = semester;
                    exam.Section = section;
                    exam.ClassSchedule = classSchedule;
                    exam.RequiresApproval = requiresApproval;
                    exam.ScheduledStartTime = scheduledStartTime;
                    exam.ScheduledEndTime = scheduledEndTime;
                    exam.ActivatedAt = DateTime.Now;
                    exam.AutoArchiveRule = autoArchiveRule;
                    exam.AutoArchiveBufferMinutes = autoArchiveBufferMinutes;

                    if (exam.Questions != null && exam.Questions.Any())
                    {
                        context.Questions.RemoveRange(exam.Questions);
                    }
                }
                else
                {
                    exam = new Exam
                    {
                        ExamId = existingExamId.Value,
                        InstructorId = instructorId,
                        ExamName = examName,
                        CourseCode = courseCode,
                        ExamCode = generatedExamCode,
                        DurationMinutes = timeLimitMinutes,
                        Status = "active",
                        CreatedAt = DateTime.Now,
                        RandomizeQuestions = randomizeQuestions,
                        Instructions = serializedInstructions,
                        AcademicYear = academicYear,
                        Semester = semester,
                        Section = section,
                        ClassSchedule = classSchedule,
                        RequiresApproval = requiresApproval,
                        ScheduledStartTime = scheduledStartTime,
                        ScheduledEndTime = scheduledEndTime,
                        ActivatedAt = DateTime.Now,
                        AutoArchiveRule = autoArchiveRule,
                        AutoArchiveBufferMinutes = autoArchiveBufferMinutes
                    };
                    context.Exams.Add(exam);
                }
            }
            else
            {
                exam = new Exam
                {
                    ExamId = Guid.NewGuid(),
                    InstructorId = instructorId,
                    ExamName = examName,
                    CourseCode = courseCode,
                    ExamCode = generatedExamCode,
                    DurationMinutes = timeLimitMinutes,
                    Status = "active",
                    CreatedAt = DateTime.Now,
                    RandomizeQuestions = randomizeQuestions,
                    Instructions = serializedInstructions,
                    AcademicYear = academicYear,
                    Semester = semester,
                    Section = section,
                    ClassSchedule = classSchedule,
                    RequiresApproval = requiresApproval,
                    ScheduledStartTime = scheduledStartTime,
                    ScheduledEndTime = scheduledEndTime,
                    ActivatedAt = DateTime.Now,
                    AutoArchiveRule = autoArchiveRule,
                    AutoArchiveBufferMinutes = autoArchiveBufferMinutes
                };
                context.Exams.Add(exam);
            }

            int orderIndex = 1;
            var originalIdToQuestionId = new Dictionary<string, Guid>();

            foreach (var examSection in sections)
            {
                foreach (var q in examSection.Questions)
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
                        CorrectAnswer = normalizedType switch
                        {
                            "true_false" => q.IsTrue == true ? "True"
                                           : q.IsTrue == false ? "False"
                                           : q.CorrectAnswer,
                            "identification" => !string.IsNullOrWhiteSpace(q.ExpectedAnswer)
                                           ? q.ExpectedAnswer
                                           : q.CorrectAnswer,
                            _ => q.CorrectAnswer
                        },
                        ProgrammingLanguage = q.ProgrammingLanguage,
                        StarterCode = q.StarterCode,
                        Points = (decimal)q.Points,
                        SectionName = !string.IsNullOrWhiteSpace(examSection.Name) ? examSection.Name : (sections.FirstOrDefault(s => s.Id == q.SectionId)?.Name ?? "General"),
                        SectionInstructions = !string.IsNullOrWhiteSpace(examSection.Instructions) ? examSection.Instructions : null,
                        OrderIndex = orderIndex++,
                        GradingMethod = rubricData.ContainsKey(q.Id) ? "manual" : "objective",
                        RequiresRubric = rubricData.ContainsKey(q.Id)
                    };

                    context.Questions.Add(question);
                    originalIdToQuestionId[q.Id] = question.QuestionId;

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

            // ── Persist rubric data collected from AIRubricModal ──────────
            var rubricService = new RubricService();

            foreach (var (originalQuestionId, rubric) in rubricData)
            {
                if (!originalIdToQuestionId.TryGetValue(originalQuestionId, out var savedQuestionId))
                    continue;

                await rubricService.SaveRubricForQuestionAsync(
                    savedQuestionId,
                    rubric,
                    rubric.AttachmentFilePath,
                    instructorId);
            }

            return exam;
        }
    }
}

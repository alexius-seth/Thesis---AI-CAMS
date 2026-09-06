using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    /// <summary>
    /// Generates a Strengths/Weaknesses assessment for one student's single
    /// exam attempt, using Gemini to interpret their actual per-question
    /// performance (reusing ExamScoresService.GetStudentAnswerDetailsAsync
    /// for the underlying data, so this stays consistent with what
    /// ExamScoresPage already shows).
    ///
    /// Saved into the existing AiAssessment table (StudentId, ExamId,
    /// Strengths, Weaknesses, AiSummary) — one row per student per exam,
    /// matching that table's shape. Scope is deliberately per-exam only for
    /// now; aggregating across a student's whole history is a separate,
    /// not-yet-built feature.
    ///
    /// This is instructor-triggered (called from a "Generate Assessment"
    /// button), not automatic — grading should be finalized first so the
    /// assessment reflects real, confirmed scores rather than AI-suggested
    /// ones still pending review.
    /// </summary>
    public class PerformanceAssessmentService
    {
        // Must match GeminiRubricGradingService.ModelEndpoint / AiJobService.GeminiModelName.
        private const string ModelEndpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent";
        private const string GeminiModelName = "gemini-3.5-flash";

        private static readonly HttpClient _http = new();

        public async Task<PerformanceAssessmentResult> GenerateAssessmentAsync(Guid attemptId)
        {
            using var context = new AppDbContext();

            var attempt = await context.StudentExamAttempts
                .Include(a => a.Student)
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

            if (attempt?.Student == null || attempt.Exam == null)
                throw new InvalidOperationException("Attempt, student, or exam could not be found.");

            // Reuse the same per-question breakdown ExamScoresPage already
            // displays, so this assessment is grounded in the exact same
            // data an instructor can see and verify themselves.
            var scoresService = new ExamScoresService();
            var questionDetails = await scoresService.GetStudentAnswerDetailsAsync(attemptId);

            if (questionDetails.Count == 0)
                throw new InvalidOperationException("No graded questions found for this attempt yet.");

            string apiKey = App.Configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Gemini API key not set. Run: dotnet user-secrets set \"Gemini:ApiKey\" \"your-key\"");

            var promptText = BuildPrompt(attempt.Student, attempt.Exam, questionDetails);

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new object[] { new { text = promptText } } }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    response_mime_type = "application/json"
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ModelEndpoint}?key={apiKey}")
            {
                Content = JsonContent.Create(requestBody)
            };

            var response = await _http.SendAsync(httpRequest);
            string rawResponseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Gemini API call failed ({(int)response.StatusCode} {response.StatusCode}): {rawResponseBody}");

            var envelope = JsonSerializer.Deserialize<GeminiApiResponse>(rawResponseBody)
                ?? throw new InvalidOperationException("Gemini returned an empty/unparseable response.");

            string? modelJsonText = envelope.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(modelJsonText))
                throw new InvalidOperationException("Gemini response contained no assessment content.");

            AssessmentModelOutput parsed;
            try
            {
                string cleanedJson = ExtractBalancedJsonObject(modelJsonText);
                parsed = JsonSerializer.Deserialize<AssessmentModelOutput>(cleanedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new JsonException("Deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Gemini did not return valid assessment JSON. Raw text was:\n{modelJsonText}", ex);
            }

            string strengthsText = string.Join("\n", parsed.Strengths.Select(s => $"• {s}"));
            string weaknessesText = string.Join("\n", parsed.Weaknesses.Select(w => $"• {w}"));

            Guid modelId = await EnsureGeminiModelAsync(context);

            // One row per student per exam — replace if one already exists,
            // so re-generating (e.g. after a grade correction) doesn't pile up duplicates.
            var existing = await context.Set<AiAssessment>()
                .FirstOrDefaultAsync(a => a.StudentId == attempt.StudentId && a.ExamId == attempt.ExamId);

            if (existing != null)
            {
                existing.Strengths = strengthsText;
                existing.Weaknesses = weaknessesText;
                existing.AiSummary = parsed.Summary;
                existing.RawAiResponse = rawResponseBody;
                existing.ModelId = modelId;
                existing.GeneratedAt = DateTime.Now;
            }
            else
            {
                context.Add(new AiAssessment
                {
                    AssessmentId = Guid.NewGuid(),
                    StudentId = attempt.StudentId,
                    ExamId = attempt.ExamId,
                    Strengths = strengthsText,
                    Weaknesses = weaknessesText,
                    AiSummary = parsed.Summary,
                    RawAiResponse = rawResponseBody,
                    ModelId = modelId,
                    GeneratedAt = DateTime.Now
                });
            }

            await context.SaveChangesAsync();

            return new PerformanceAssessmentResult
            {
                Strengths = parsed.Strengths,
                Weaknesses = parsed.Weaknesses,
                Summary = parsed.Summary
            };
        }

        // ── Load a previously-generated assessment, if one exists ────────
        // For displaying on StudentReportPage without re-calling Gemini every time.
        public async Task<PerformanceAssessmentResult?> GetExistingAssessmentAsync(string studentId, Guid examId)
        {
            using var context = new AppDbContext();

            var existing = await context.Set<AiAssessment>()
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.ExamId == examId);

            if (existing == null) return null;

            return new PerformanceAssessmentResult
            {
                Strengths = SplitBullets(existing.Strengths),
                Weaknesses = SplitBullets(existing.Weaknesses),
                Summary = existing.AiSummary ?? string.Empty,
                GeneratedAt = existing.GeneratedAt
            };
        }

        private static List<string> SplitBullets(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimStart('•', ' '))
                .Where(line => line.Length > 0)
                .ToList();
        }

        // ── Prompt construction ──────────────────────────────────────────
        private static string BuildPrompt(Student student, Exam exam, List<StudentAnswerDetailDto> questions)
        {
            var questionSummaries = questions.Select(q =>
            {
                string outcome = q.IsObjective
                    ? (q.ObtainedPoints.HasValue && q.ObtainedPoints.Value >= q.MaxPoints
                        ? "Correct"
                        : q.ObtainedPoints.HasValue ? "Incorrect" : "Not graded")
                    : (q.ObtainedPoints.HasValue ? $"{q.ObtainedPoints.Value}/{q.MaxPoints} points (AI/instructor graded)" : "Not graded");

                return new
                {
                    number = q.QuestionNumber,
                    type = q.QuestionType,
                    outcome,
                    pointsEarned = q.ObtainedPoints,
                    maxPoints = q.MaxPoints
                };
            });

            var questionsJson = JsonSerializer.Serialize(questionSummaries, new JsonSerializerOptions { WriteIndented = true });

            decimal totalEarned = questions.Sum(q => q.ObtainedPoints ?? 0);
            decimal totalMax = questions.Sum(q => q.MaxPoints);

            return $$"""
                You are analyzing ONE student's performance on ONE exam, to identify genuine
                academic strengths and weaknesses — NOT to grade anything (grading is already done).

                Student: {{student.FirstName}} {{student.LastName}}
                Exam: {{exam.Title ?? exam.ExamName ?? "Untitled Exam"}}
                Overall score: {{totalEarned}} / {{totalMax}}

                PER-QUESTION RESULTS (JSON):
                {{questionsJson}}

                INSTRUCTIONS:
                1. Identify 2-4 concrete STRENGTHS based on which questions/question types this student
                   handled well. Be specific (e.g. "Strong grasp of factual recall in identification
                   questions" rather than a generic "did well").
                2. Identify 2-4 concrete WEAKNESSES based on which questions/question types this student
                   struggled with. Be specific and constructive — this is meant to help the student
                   improve, not just criticize.
                3. Do NOT fabricate strengths/weaknesses not supported by the data above. If performance
                   is uniformly strong or uniformly weak, say so honestly rather than inventing false balance.
                4. Write a brief (2-3 sentence) overall summary.
                5. Return ONLY valid JSON matching this exact schema, no markdown fences, no extra commentary:

                {
                  "strengths": [string, ...],
                  "weaknesses": [string, ...],
                  "summary": string
                }
                """;
        }

        // Gemini's structured output mode greatly reduces malformed JSON, but
        // doesn't guarantee zero stray characters — e.g. an extra trailing
        // "}" after an otherwise-valid object. System.Text.Json rejects the
        // ENTIRE response if there's anything after the valid JSON, even one
        // stray character. This walks brace depth to find exactly where the
        // real object starts and ends, discarding anything before/after it.
        private static string ExtractBalancedJsonObject(string raw)
        {
            int start = raw.IndexOf('{');
            if (start < 0) return raw; // no object found at all — let deserialization fail naturally

            int depth = 0;
            for (int i = start; i < raw.Length; i++)
            {
                if (raw[i] == '{') depth++;
                else if (raw[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return raw.Substring(start, i - start + 1);
                }
            }

            return raw.Substring(start); // unbalanced — return what we have, let deserialization report the real error
        }

        private static async Task<Guid> EnsureGeminiModelAsync(AppDbContext context)
        {
            var existing = await context.Set<AiModel>()
                .FirstOrDefaultAsync(m => m.ModelName == GeminiModelName);

            if (existing != null) return existing.ModelId;

            var model = new AiModel
            {
                ModelId = Guid.NewGuid(),
                ModelName = GeminiModelName,
                Provider = "Google",
                ModelVersion = "3.5-flash",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            context.Add(model);
            await context.SaveChangesAsync();
            return model.ModelId;
        }

        // ── Gemini API response envelope ─────────────────────────────────
        private class GeminiApiResponse
        {
            [JsonPropertyName("candidates")]
            public List<GeminiCandidate>? Candidates { get; set; }
        }

        private class GeminiCandidate
        {
            [JsonPropertyName("content")]
            public GeminiContent? Content { get; set; }
        }

        private class GeminiContent
        {
            [JsonPropertyName("parts")]
            public List<GeminiPart>? Parts { get; set; }
        }

        private class GeminiPart
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }

        private class AssessmentModelOutput
        {
            public List<string> Strengths { get; set; } = new();
            public List<string> Weaknesses { get; set; } = new();
            public string Summary { get; set; } = string.Empty;
        }
    }

    public class PerformanceAssessmentResult
    {
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public DateTime? GeneratedAt { get; set; }
    }
}

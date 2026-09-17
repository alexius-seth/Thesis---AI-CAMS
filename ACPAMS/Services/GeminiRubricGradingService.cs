using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACPAMS.Services
{
    /// <summary>
    /// Grades one student's essay/coding answer against a rubric matrix using
    /// Gemini. Optionally cross-checks the submission against an attached
    /// reference document (e.g. an answer key or marking guide PDF) by sending
    /// the PDF directly to Gemini alongside the prompt — Gemini 1.5+ reads PDF
    /// content natively (including scanned/image pages), so no separate
    /// text-extraction library is needed.
    /// </summary>
    public class GeminiRubricGradingService
    {
        // Model history: gemini-1.5-flash was retired by Google (404).
        // gemini-3.1-flash-lite hit a 403 PERMISSION_DENIED (account/billing
        // issue on the Google Cloud project, unrelated to this code). Team
        // decision: using gemini-3.5-flash. If this ever 404s/403s again,
        // check https://ai.google.dev/gemini-api/docs/changelog for the
        // current model name and swap it in here (and in
        // AiJobService.GeminiModelName) — and if it's a 403 specifically,
        // check Google Cloud Console billing/API-enablement first, since
        // that's an account issue, not a code issue.
        private static string ModelEndpoint =>
            $"https://generativelanguage.googleapis.com/v1beta/models/{AppSettingsService.GetGeminiModel()}:generateContent";

        private static readonly HttpClient _http = new();

        public async Task<AiRubricGradingResult> GradeAsync(RubricGradingRequest request)
        {
            string apiKey = AppSettingsService.GetGeminiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Gemini API key not set. Please configure it in Faculty Settings.");

            var promptText = BuildPrompt(request);

            var parts = new List<object> { new { text = promptText } };

            // Attach the reference PDF, if one was provided, as native document input.
            if (!string.IsNullOrEmpty(request.AttachmentPdfPath) && File.Exists(request.AttachmentPdfPath))
            {
                byte[] pdfBytes = await File.ReadAllBytesAsync(request.AttachmentPdfPath);
                string base64Pdf = Convert.ToBase64String(pdfBytes);

                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = "application/pdf",
                        data = base64Pdf
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = parts.ToArray() }
                },
                generationConfig = new
                {
                    // NOTE: Google's Gemini 3.x docs list temperature as a
                    // deprecated parameter (superseded by thinking_config).
                    // Leaving it here since it's still accepted for now —
                    // if grading ever seems less consistent than expected on
                    // this model line, that's the first thing to revisit
                    // (migrate to thinking_config with thinking_level="low").
                    temperature = 0.2,
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
            {
                throw new InvalidOperationException(
                    $"Gemini API call failed ({(int)response.StatusCode} {response.StatusCode}): {rawResponseBody}");
            }

            var geminiEnvelope = JsonSerializer.Deserialize<GeminiApiResponse>(rawResponseBody)
                ?? throw new InvalidOperationException("Gemini returned an empty/unparseable response.");

            string? modelJsonText = geminiEnvelope.Candidates?
                .FirstOrDefault()?.Content?.Parts?
                .FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(modelJsonText))
                throw new InvalidOperationException("Gemini response contained no gradable content.");

            AiGradingModelOutput parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<AiGradingModelOutput>(modelJsonText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new JsonException("Deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Gemini did not return valid grading JSON. Raw text was:\n{modelJsonText}", ex);
            }

            return new AiRubricGradingResult
            {
                CriteriaResults = parsed.CriteriaScores.Select(c => new AiCriterionResult
                {
                    CriteriaName = c.CriteriaName,
                    MatchedTier = c.MatchedTier,
                    AwardedScore = c.AwardedScore,
                    Justification = c.Justification
                }).ToList(),
                TotalScore = parsed.TotalScore,
                MaxScore = request.Criteria.Sum(c => c.MaxScore),
                OverallFeedback = parsed.OverallFeedback,
                AlignedWithAttachment = parsed.AlignedWithAttachment,
                RawResponseJson = rawResponseBody
            };
        }

        // ── Prompt construction ──────────────────────────────────────────
        private static string BuildPrompt(RubricGradingRequest request)
        {
            var rubricJson = JsonSerializer.Serialize(new
            {
                criteria = request.Criteria.Select(c => new
                {
                    name = c.Name,
                    maxScore = c.MaxScore,
                    tiers = c.Levels.Select(l => new
                    {
                        tierName = l.TierName,
                        scorePercentage = l.ScorePercentage,
                        descriptor = l.Descriptor
                    })
                })
            }, new JsonSerializerOptions { WriteIndented = true });

            string submissionLabel = request.IsCode
                ? $"Student's submitted code ({request.ProgrammingLanguage ?? "unspecified language"}):"
                : "Student's submitted essay answer:";

            string attachmentInstruction = request.AttachmentPdfPath != null
                ? "\nA reference document (e.g. an answer key, marking guide, or model solution) is attached as a PDF. " +
                  "Treat it as authoritative context. Cross-check whether the student's submission aligns with what the " +
                  "reference document describes, and factor that alignment directly into which tier each criterion falls under. " +
                  "Set \"alignedWithAttachment\" to true only if the submission's core content genuinely matches what the " +
                  "attached reference establishes as correct/expected; set it to false if it diverges, and explain why in " +
                  "that criterion's justification."
                : "";

            string instructorInstructionsBlock = !string.IsNullOrWhiteSpace(request.InstructorInstructions)
                ? $$"""

                    INSTRUCTOR'S GRADING INSTRUCTIONS (follow these — they express what the instructor specifically
                    wants checked or emphasized for this question, on top of the rubric matrix itself):
                    {{request.InstructorInstructions}}
                    """
                : "";

            return $$"""
                You are grading ONE student's submission for a single exam question, using ONLY the rubric matrix below
                plus the instructor's grading instructions. Do not invent criteria, tiers, or scoring rules that are not
                in the matrix. Do not apply your own general grading standards beyond what's specified here — the rubric
                and the instructor's instructions together are the sole source of truth for how this should be scored.

                QUESTION:
                {{request.QuestionText}}

                RUBRIC MATRIX (JSON):
                {{rubricJson}}
                {{instructorInstructionsBlock}}
                {{attachmentInstruction}}

                {{submissionLabel}}
                {{request.StudentAnswerText}}

                INSTRUCTIONS:
                1. Follow the instructor's grading instructions above (if any) as the guiding intent behind how strictly
                   or leniently to interpret each rubric tier.
                2. For EVERY criterion in the rubric matrix, select exactly one tier that best matches the student's work
                   for that criterion, and award the score associated with that tier.
                3. Write a brief (1-2 sentence) justification per criterion explaining why that tier was chosen, referencing
                   specific evidence from the student's submission.
                4. Sum the awarded scores across all criteria for the total score.
                5. Provide brief overall feedback (2-4 sentences) summarizing strengths and weaknesses.
                6. Return ONLY valid JSON matching this exact schema, with no markdown code fences and no extra commentary:

                {
                  "criteriaScores": [
                    { "criteriaName": string, "matchedTier": string, "awardedScore": number, "justification": string }
                  ],
                  "totalScore": number,
                  "overallFeedback": string,
                  "alignedWithAttachment": boolean or null (null if no attachment was provided)
                }
                """;
        }

        // ── Gemini API response envelope (only the fields we need) ───────
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

        // ── What we ask the model to produce (deserialized from its JSON text) ──
        private class AiGradingModelOutput
        {
            public List<AiCriterionModelOutput> CriteriaScores { get; set; } = new();
            public decimal TotalScore { get; set; }
            public string OverallFeedback { get; set; } = string.Empty;
            public bool? AlignedWithAttachment { get; set; }
        }

        private class AiCriterionModelOutput
        {
            public string CriteriaName { get; set; } = string.Empty;
            public string MatchedTier { get; set; } = string.Empty;
            public decimal AwardedScore { get; set; }
            public string Justification { get; set; } = string.Empty;
        }
    }

    // ── Public request/result DTOs ───────────────────────────────────────

    public class RubricGradingRequest
    {
        public string QuestionText { get; set; } = string.Empty;
        public string StudentAnswerText { get; set; } = string.Empty;
        public bool IsCode { get; set; }
        public string? ProgrammingLanguage { get; set; }
        public List<RubricCriterionDto> Criteria { get; set; } = new();

        /// <summary>The faculty's free-text "Grading Instructions / AI Prompt" field
        /// from AIRubricModal (AIPromptTextBox). Read fresh at grading time, alongside
        /// the rubric matrix and attachment — not pre-processed separately.</summary>
        public string InstructorInstructions { get; set; } = string.Empty;

        /// <summary>Full path to the rubric reference PDF, if one was attached. Null if none.</summary>
        public string? AttachmentPdfPath { get; set; }
    }

    public class RubricCriterionDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal MaxScore { get; set; }
        public List<RubricTierDto> Levels { get; set; } = new();
    }

    public class RubricTierDto
    {
        public string TierName { get; set; } = string.Empty;
        public decimal ScorePercentage { get; set; }
        public string Descriptor { get; set; } = string.Empty;
    }

    public class AiRubricGradingResult
    {
        public List<AiCriterionResult> CriteriaResults { get; set; } = new();
        public decimal TotalScore { get; set; }
        public decimal MaxScore { get; set; }
        public string OverallFeedback { get; set; } = string.Empty;

        /// <summary>Null if no attachment was provided; otherwise whether Gemini judged the
        /// submission to align with the attached reference document.</summary>
        public bool? AlignedWithAttachment { get; set; }

        /// <summary>Full raw JSON response from Gemini, stored for audit/debugging
        /// (maps to AiEvaluation.AiResponseRaw in the schema).</summary>
        public string RawResponseJson { get; set; } = string.Empty;
    }

    public class AiCriterionResult
    {
        public string CriteriaName { get; set; } = string.Empty;
        public string MatchedTier { get; set; } = string.Empty;
        public decimal AwardedScore { get; set; }
        public string Justification { get; set; } = string.Empty;
    }
}
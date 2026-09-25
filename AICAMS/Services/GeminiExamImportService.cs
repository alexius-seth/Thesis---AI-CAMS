using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ACPAMS.Services
{
    /// <summary>
    /// AI-powered exam import service that converts text exam files into structured domain DTOs via Gemini.
    /// </summary>
    public class GeminiExamImportService
    {
        // Current valid fallback endpoints — deprecated models (e.g. gemini-2.5-flash, gemini-1.5-flash) removed
        private static readonly string[] ModelCandidates = new[]
        {
            "gemini-3.6-flash",
            "gemini-3.1-flash-lite"
        };

        private static string BuildEndpointForModel(string model) =>
            $"https://generativelanguage.googleapis.com/v1beta/models/{AppSettingsService.NormalizeModelName(model)}:generateContent";

        private static bool IsValidTextModel(string model) =>
            !string.IsNullOrWhiteSpace(model) &&
            !model.Contains("-tts", StringComparison.OrdinalIgnoreCase) &&
            !model.Contains("imagen", StringComparison.OrdinalIgnoreCase) &&
            !model.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
            !model.Contains("gemma", StringComparison.OrdinalIgnoreCase);

        private static readonly HttpClient _http = new();

        public async Task<List<ImportedSectionDto>> ParseExamTextAsync(string rawFileText)
        {
            if (string.IsNullOrWhiteSpace(rawFileText))
                throw new InvalidOperationException("The imported file is empty.");

            string apiKey = AppSettingsService.GetGeminiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Gemini API key not set. Please configure it in Faculty Settings.");

            // Read the active model directly from saved UI configuration
            string activeModel = AppSettingsService.GetGeminiModel();
            if (string.IsNullOrWhiteSpace(activeModel))
                activeModel = "gemini-3.5-flash";

            var promptText = BuildPrompt(rawFileText);

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new object[] { new { text = promptText } } }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    response_mime_type = "application/json"
                }
            };

            // Dynamically assemble candidates starting with the user's active configured model
            var candidateModels = (new[] { activeModel }.Concat(ModelCandidates))
                .Where(IsValidTextModel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            const int maxRetriesPerModel = 1; // 1 quick retry per model before failing over
            TimeSpan retryDelay = TimeSpan.FromMilliseconds(1500);
            HttpResponseMessage? response = null;

            string primaryModel = activeModel;
            string primaryErrorBody = string.Empty;
            int primaryStatusCode = 0;

            var attemptHistory = new List<string>();

            foreach (var model in candidateModels)
            {
                bool isPrimary = string.Equals(model, primaryModel, StringComparison.OrdinalIgnoreCase);
                string endpoint = BuildEndpointForModel(model);

                bool cycleToNextModel = false;

                for (int attempt = 1; attempt <= maxRetriesPerModel + 1; attempt++)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}?key={apiKey}")
                    {
                        Content = JsonContent.Create(requestBody)
                    };

                    try
                    {
                        response = await _http.SendAsync(request, cts.Token);

                        int statusCode = (int)response.StatusCode;
                        if (response.IsSuccessStatusCode)
                        {
                            string successLog = $"[ExamImport] SUCCESS: Model '{model}' (Attempt {attempt}) succeeded with HTTP {statusCode}.";
                            System.Diagnostics.Debug.WriteLine(successLog);
                            Console.WriteLine(successLog);
                            attemptHistory.Add(successLog);
                            break;
                        }

                        string responseBody = await response.Content.ReadAsStringAsync();
                        string cleanMsg = ExtractApiErrorMessage(responseBody);

                        string attemptLog = $"[ExamImport] FAIL: Model '{model}' (Attempt {attempt}) returned HTTP {statusCode} ({response.StatusCode}). Response: {cleanMsg}";
                        System.Diagnostics.Debug.WriteLine(attemptLog);
                        Console.WriteLine(attemptLog);
                        attemptHistory.Add(attemptLog);

                        // Stop masking initial error: preserve the primary model's error details
                        if (isPrimary && (primaryStatusCode == 0 || primaryStatusCode == 404))
                        {
                            primaryStatusCode = statusCode;
                            primaryErrorBody = !string.IsNullOrWhiteSpace(cleanMsg) ? cleanMsg : responseBody;
                        }

                        // Unrecoverable status codes (400, 401, 403)
                        if (statusCode == 400 || statusCode == 401 || statusCode == 403)
                        {
                            throw new InvalidOperationException($"Gemini API error ({statusCode}): {cleanMsg}");
                        }

                        // Model not found (404) -> cycle immediately to next model
                        if (statusCode == 404)
                        {
                            cycleToNextModel = true;
                            break;
                        }

                        // Transient 500, 503, 429 -> 1 quick retry (1.5s) then fail over
                        if (statusCode == 500 || statusCode == 503 || statusCode == 429)
                        {
                            if (attempt <= maxRetriesPerModel)
                            {
                                await Task.Delay(retryDelay, cts.Token);
                                continue;
                            }
                            cycleToNextModel = true;
                            break;
                        }

                        if (attempt <= maxRetriesPerModel)
                        {
                            await Task.Delay(retryDelay, cts.Token);
                            continue;
                        }
                        cycleToNextModel = true;
                        break;
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested)
                    {
                        string timeoutLog = $"[ExamImport] TIMEOUT: Model '{model}' (Attempt {attempt}) timed out after 20s.";
                        System.Diagnostics.Debug.WriteLine(timeoutLog);
                        Console.WriteLine(timeoutLog);
                        attemptHistory.Add(timeoutLog);

                        if (isPrimary && (primaryStatusCode == 0 || primaryStatusCode == 404))
                        {
                            primaryStatusCode = 408;
                            primaryErrorBody = "Request timed out after 20 seconds.";
                        }

                        cycleToNextModel = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        string exLog = $"[ExamImport] EXCEPTION: Model '{model}' (Attempt {attempt}) error: {ex.Message}";
                        System.Diagnostics.Debug.WriteLine(exLog);
                        Console.WriteLine(exLog);
                        attemptHistory.Add(exLog);

                        if (isPrimary && (primaryStatusCode == 0 || primaryStatusCode == 404))
                        {
                            primaryStatusCode = 0;
                            primaryErrorBody = ex.Message;
                        }

                        if (attempt <= maxRetriesPerModel)
                        {
                            try { await Task.Delay(retryDelay); } catch { }
                            continue;
                        }
                        cycleToNextModel = true;
                        break;
                    }
                }

                if (response != null && response.IsSuccessStatusCode)
                {
                    break;
                }

                if (!cycleToNextModel)
                {
                    break;
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                // Surface the primary configured model's error rather than solely reporting the final fallback's message
                string primaryErrorSummary = primaryStatusCode != 0
                    ? $"Primary model '{primaryModel}' failed with HTTP {primaryStatusCode}: {primaryErrorBody}"
                    : $"Primary model '{primaryModel}' failed: {primaryErrorBody}";

                string allLogs = string.Join("\n", attemptHistory);
                throw new InvalidOperationException(
                    $"All Gemini candidate models failed to import the file.\n\n{primaryErrorSummary}\n\nExecution history:\n{allLogs}");
            }

            string rawResponseBody = await response.Content.ReadAsStringAsync();

            var geminiEnvelope = JsonSerializer.Deserialize<GeminiApiResponse>(rawResponseBody)
                ?? throw new InvalidOperationException("Gemini returned an empty or unparseable response.");

            var candidateParts = geminiEnvelope.Candidates?
                .FirstOrDefault()?.Content?.Parts;

            string? modelJsonText = null;
            if (candidateParts != null && candidateParts.Count > 0)
            {
                var jsonPart = candidateParts.LastOrDefault(p => p.Thought != true && !string.IsNullOrWhiteSpace(p.Text) && (p.Text.Contains('{') || p.Text.Contains('[')));
                if (jsonPart != null)
                {
                    modelJsonText = jsonPart.Text;
                }
                else
                {
                    var nonThoughtTexts = candidateParts.Where(p => p.Thought != true && !string.IsNullOrWhiteSpace(p.Text)).Select(p => p.Text).ToList();
                    modelJsonText = nonThoughtTexts.Count > 0
                        ? string.Join("\n", nonThoughtTexts)
                        : candidateParts.LastOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?.Text;
                }
            }

            if (string.IsNullOrWhiteSpace(modelJsonText))
                throw new InvalidOperationException("Gemini response contained no importable content.");

            string cleaned = ExtractBalancedJson(modelJsonText);
            if (string.IsNullOrWhiteSpace(cleaned))
                throw new InvalidOperationException("Failed to locate valid JSON structure in Gemini response.");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            List<ImportedSectionDto> parsedSections = new();

            try
            {
                using var doc = JsonDocument.Parse(cleaned, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

                JsonElement sectionsElement = default;

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var firstObj = doc.RootElement.EnumerateArray().FirstOrDefault();
                    if (firstObj.ValueKind == JsonValueKind.Object && TryGetPropertyCaseInsensitive(firstObj, "sections", out var s) && s.ValueKind == JsonValueKind.Array)
                    {
                        sectionsElement = s;
                    }
                    else
                    {
                        sectionsElement = doc.RootElement;
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetPropertyCaseInsensitive(doc.RootElement, "sections", out var s) && s.ValueKind == JsonValueKind.Array)
                    {
                        sectionsElement = s;
                    }
                    else if (HasAnyPropertyCaseInsensitive(doc.RootElement, "sectionTitle", "section_title", "questions", "name"))
                    {
                        var single = JsonSerializer.Deserialize<ImportedSectionDto>(doc.RootElement.GetRawText(), jsonOptions);
                        if (single != null)
                        {
                            parsedSections.Add(single);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("JSON object does not contain a 'sections' array.");
                    }
                }

                if (sectionsElement.ValueKind == JsonValueKind.Array)
                {
                    var deserialized = JsonSerializer.Deserialize<List<ImportedSectionDto>>(sectionsElement.GetRawText(), jsonOptions);
                    if (deserialized != null)
                    {
                        parsedSections.AddRange(deserialized);
                    }
                }
            }
            catch (JsonException ex)
            {
                try
                {
                    string wrappedArray = cleaned.StartsWith("[") ? cleaned : $"[{Regex.Replace(cleaned, @"}\s*\{", "},{")}]";
                    var consecutive = JsonSerializer.Deserialize<List<ImportedSectionDto>>(wrappedArray, jsonOptions);
                    if (consecutive != null && consecutive.Count > 0)
                    {
                        parsedSections.AddRange(consecutive);
                    }
                }
                catch
                {
                    throw new InvalidOperationException(
                        $"Gemini returned malformed JSON: {ex.Message}\nCleaned payload was:\n{cleaned}\n\nRaw text was:\n{modelJsonText}", ex);
                }
            }

            if (parsedSections.Count == 0 || !parsedSections.Any(s => s.Questions != null && s.Questions.Count > 0))
            {
                throw new InvalidOperationException(
                    $"Gemini did not return valid exam sections or questions. Cleaned payload was:\n{cleaned}\n\nRaw text was:\n{modelJsonText}");
            }

            parsedSections = SanitizeAndSplitSections(parsedSections);

            return parsedSections;
        }

        private static string ExtractBalancedJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            int firstBrace = text.IndexOf('{');
            int firstBracket = text.IndexOf('[');
            if (firstBrace == -1 && firstBracket == -1) return text;

            int start = (firstBrace != -1 && firstBracket != -1)
                ? Math.Min(firstBrace, firstBracket)
                : (firstBrace != -1 ? firstBrace : firstBracket);

            char openChar = text[start];
            char closeChar = openChar == '{' ? '}' : ']';
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == openChar) depth++;
                else if (c == closeChar)
                {
                    depth--;
                    if (depth == 0) return text.Substring(start, i - start + 1);
                }
            }
            return text.Substring(start);
        }

        private static string ExtractApiErrorMessage(string rawError)
        {
            if (string.IsNullOrWhiteSpace(rawError)) return string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(rawError);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msg))
                {
                    return msg.GetString() ?? rawError;
                }
            }
            catch { }
            return rawError.Trim();
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = prop.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }

        private static bool HasAnyPropertyCaseInsensitive(JsonElement element, params string[] propertyNames)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    foreach (var target in propertyNames)
                    {
                        if (string.Equals(prop.Name, target, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            return false;
        }

        private static List<ImportedSectionDto> SanitizeAndSplitSections(List<ImportedSectionDto> sections)
        {
            var result = new List<ImportedSectionDto>();

            foreach (var section in sections)
            {
                if (section.Questions == null || section.Questions.Count == 0)
                    continue;

                foreach (var q in section.Questions)
                {
                    SanitizeQuestion(q, section);
                }

                var grouped = section.Questions
                    .GroupBy(q => NormalizeQuestionType(q, section.QuestionType))
                    .ToList();

                if (grouped.Count <= 1)
                {
                    var singleGroup = grouped.FirstOrDefault();
                    if (singleGroup != null)
                    {
                        section.QuestionType = singleGroup.Key;
                    }
                    result.Add(section);
                }
                else
                {
                    foreach (var group in grouped)
                    {
                        var splitSection = new ImportedSectionDto
                        {
                            SectionTitle = $"{section.SectionTitle} ({FormatTypeForTitle(group.Key)})",
                            Name = $"{section.Name} ({FormatTypeForTitle(group.Key)})",
                            QuestionType = group.Key,
                            Instructions = section.Instructions,
                            Questions = group.ToList()
                        };
                        result.Add(splitSection);
                    }
                }
            }

            return result;
        }

        private static void SanitizeQuestion(ImportedQuestionDto q, ImportedSectionDto parentSection)
        {
            string[] knownInstructions = new[]
            {
                "Select the single correct answer from the provided choices.",
                "List the required terms or concepts accurately.",
                "Provide detailed, structured technical explanations based on the prompt.",
                "Write robust, syntactically correct implementations in one of the supported languages: C, Python, or Java."
            };

            if (!string.IsNullOrWhiteSpace(q.Prompt))
            {
                string cleanPrompt = Regex.Replace(
                    q.Prompt,
                    @"^(?:Section\s+\d+[:\.\s]*|Part\s+[IVXLCDM\d]+[:\.\s—–\-]*|Question\s+\d+[:\.\s]*|Prompt:\s*|Points:\s*\d+[:\.\s]*)+",
                    "",
                    RegexOptions.IgnoreCase).Trim();

                foreach (var inst in knownInstructions)
                {
                    if (cleanPrompt.StartsWith(inst, StringComparison.OrdinalIgnoreCase))
                    {
                        cleanPrompt = cleanPrompt.Substring(inst.Length).Trim();
                        cleanPrompt = Regex.Replace(
                            cleanPrompt,
                            @"^(?:Section\s+\d+[:\.\s]*|Part\s+[IVXLCDM\d]+[:\.\s—–\-]*|Question\s+\d+[:\.\s]*|Prompt:\s*|Points:\s*\d+[:\.\s]*)+",
                            "",
                            RegexOptions.IgnoreCase).Trim();
                    }
                }

                int rubricIdx = cleanPrompt.IndexOf("Evaluation Rubric:", StringComparison.OrdinalIgnoreCase);
                if (rubricIdx >= 0)
                {
                    if (string.IsNullOrWhiteSpace(q.Rubric))
                        q.Rubric = cleanPrompt.Substring(rubricIdx).Trim();
                    cleanPrompt = cleanPrompt.Substring(0, rubricIdx).Trim();
                }

                int choicesIdx = cleanPrompt.IndexOf("Choices:", StringComparison.OrdinalIgnoreCase);
                if (choicesIdx >= 0)
                {
                    cleanPrompt = cleanPrompt.Substring(0, choicesIdx).Trim();
                }

                q.Prompt = cleanPrompt;
            }

            var detectedType = NormalizeQuestionType(q, parentSection.QuestionType);
            q.QuestionType = detectedType;

            if (detectedType == "Coding")
            {
                q.Options = null;
                if (string.IsNullOrWhiteSpace(q.TargetLanguage))
                {
                    q.TargetLanguage = !string.IsNullOrWhiteSpace(q.ProgrammingLanguage)
                        ? q.ProgrammingLanguage
                        : (q.StarterCode?.Contains("#include") == true ? "C" : "Python");
                }
            }
            else if (detectedType == "TrueOrFalse")
            {
                if (!q.IsTrue.HasValue && !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                {
                    var ans = q.CorrectAnswer.Trim();
                    if (ans.Equals("True", StringComparison.OrdinalIgnoreCase) || ans.Equals("T", StringComparison.OrdinalIgnoreCase))
                        q.IsTrue = true;
                    else if (ans.Equals("False", StringComparison.OrdinalIgnoreCase) || ans.Equals("F", StringComparison.OrdinalIgnoreCase))
                        q.IsTrue = false;
                }
            }
            else if (detectedType == "Identification")
            {
                if (string.IsNullOrWhiteSpace(q.ExpectedAnswer) && !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                {
                    q.ExpectedAnswer = q.CorrectAnswer.Trim();
                }
            }
        }

        private static string NormalizeQuestionType(ImportedQuestionDto q, string? sectionType)
        {
            if (!string.IsNullOrWhiteSpace(q.QuestionType))
            {
                var lower = q.QuestionType.Trim().ToLowerInvariant();
                if (lower.Contains("multiple") && lower.Contains("choice") || lower == "multiplechoice" || lower == "mcq")
                    return "MultipleChoice";
                if (lower.Contains("true") && lower.Contains("false") || lower == "trueorfalse" || lower == "tf" || lower == "t/f")
                    return "TrueOrFalse";
                if (lower.Contains("identification") || lower == "id" || lower.Contains("fill"))
                    return "Identification";
                if (lower.Contains("enumeration") || lower == "enum")
                    return "Enumeration";
                if (lower.Contains("essay"))
                    return "Essay";
                if (lower.Contains("coding") || lower == "code" || lower == "programming")
                    return "Coding";
            }

            if (q.Options != null && q.Options.Count >= 2)
                return "MultipleChoice";

            if (q.TestCases != null && q.TestCases.Count > 0 || !string.IsNullOrWhiteSpace(q.StarterCode))
                return "Coding";

            if (q.ExpectedAnswers != null && q.ExpectedAnswers.Count > 1)
                return "Enumeration";

            if (!string.IsNullOrWhiteSpace(sectionType))
            {
                var lowerSec = sectionType.Trim().ToLowerInvariant();
                if (lowerSec.Contains("multiple") && lowerSec.Contains("choice")) return "MultipleChoice";
                if (lowerSec.Contains("true") && lowerSec.Contains("false")) return "TrueOrFalse";
                if (lowerSec.Contains("identification")) return "Identification";
                if (lowerSec.Contains("enumeration")) return "Enumeration";
                if (lowerSec.Contains("essay")) return "Essay";
                if (lowerSec.Contains("coding")) return "Coding";
            }

            return "Identification";
        }

        private static string FormatTypeForTitle(string type)
        {
            return type switch
            {
                "MultipleChoice" => "Multiple Choice",
                "TrueOrFalse" => "True or False",
                _ => type
            };
        }

        private static string BuildPrompt(string rawFileText)
        {
            return $$"""
                You are converting a plain-text exam question file into structured JSON for an exam-authoring system.
                Analyze the provided raw exam text, segment it into homogeneous sections based on question types, and structure every question with its metadata.

                Supported question types:
                - "MultipleChoice": Has options A, B, C, D (or more). Extract choices into optionA, optionB, optionC, optionD, and options array. Set correctAnswer to single uppercase letter.
                - "TrueOrFalse": True/False judgment questions. Set isTrue to true or false, and correctAnswer to "True" or "False".
                - "Identification": Short direct factual answer. Set expectedAnswer and correctAnswer to the expected term.
                - "Enumeration": Multiple terms or items expected. Provide the items in expectedAnswers array.
                - "Essay": Conceptual or analytical prompt. Include rubric if stated in the text.
                - "Coding": Programming question. Extract targetLanguage (C, Python, Java), starterCode stub, and testCases with input and expectedOutput.

                Return ONLY valid JSON with this exact schema:
                {
                  "sections": [
                    {
                      "sectionTitle": "Section title string",
                      "instructions": "Section instructions string or null",
                      "questionType": "MultipleChoice" | "TrueOrFalse" | "Identification" | "Enumeration" | "Essay" | "Coding",
                      "questions": [
                        {
                          "questionNumber": 1,
                          "prompt": "The question prompt text",
                          "points": 5,
                          "questionType": "MultipleChoice" | "TrueOrFalse" | "Identification" | "Enumeration" | "Essay" | "Coding",
                          "options": ["Option A", "Option B", "Option C", "Option D"],
                          "optionA": "Option A",
                          "optionB": "Option B",
                          "optionC": "Option C",
                          "optionD": "Option D",
                          "correctAnswer": "A",
                          "expectedAnswer": null,
                          "expectedAnswers": [],
                          "isTrue": null,
                          "targetLanguage": null,
                          "starterCode": null,
                          "rubric": null,
                          "testCases": []
                        }
                      ]
                    }
                  ]
                }

                RAW EXAM FILE CONTENT:
                {{rawFileText}}
                """;
        }

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
            [JsonPropertyName("thought")]
            public bool? Thought { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}


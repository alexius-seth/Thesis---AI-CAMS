using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ACPAMS.Services
{
    public static class AppSettingsService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        private static readonly string KeyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gemini.key.dat");

        // ─── Grading Thresholds (Defaults) ──────────────────────────────────
        public static decimal PassingGrade { get; set; } = 75.0m;
        public static decimal ExcellentThreshold { get; set; } = 90.0m;
        public static decimal GoodThreshold { get; set; } = 80.0m;
        public static decimal FairThreshold { get; set; } = 70.0m;
        public static int IntegrityFlagCap { get; set; } = 10;

        // ─── Monitoring Sensitivity (Defaults) ──────────────────────────────
        public static double HeadYawThreshold { get; set; } = 25.0;
        public static double HeadPitchThreshold { get; set; } = 20.0;
        public static int NoFaceFrameThreshold { get; set; } = 45;
        public static int WiggleRoomFrames { get; set; } = 60;
        public static int AlertCooldownSeconds { get; set; } = 10;
        public static double ScoreDecayRate { get; set; } = 0.02;

        // ─── Gemini AI Model (Defaults) ─────────────────────────────────────
        public static string GeminiModel { get; set; } = "gemini-2.5-flash-lite";

        static AppSettingsService()
        {
            LoadSettings();
        }

        public static void Initialize()
        {
            LoadSettings();

            // Decrypt stored key if present and update runtime config
            string storedKey = GetGeminiApiKey();
            if (!string.IsNullOrWhiteSpace(storedKey) && App.Configuration != null)
            {
                App.Configuration["Gemini:ApiKey"] = storedKey;
            }

            if (App.Configuration != null)
            {
                App.Configuration["Gemini:Model"] = GetGeminiModel();
            }
        }

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string content = File.ReadAllText(SettingsFilePath);
                    var json = JsonNode.Parse(content) as JsonObject;
                    if (json != null)
                    {
                        if (json["Grading"] is JsonObject grading)
                        {
                            if (grading["PassingGrade"] != null) PassingGrade = (decimal)grading["PassingGrade"]!;
                            if (grading["ExcellentThreshold"] != null) ExcellentThreshold = (decimal)grading["ExcellentThreshold"]!;
                            if (grading["GoodThreshold"] != null) GoodThreshold = (decimal)grading["GoodThreshold"]!;
                            if (grading["FairThreshold"] != null) FairThreshold = (decimal)grading["FairThreshold"]!;
                            if (grading["IntegrityFlagCap"] != null) IntegrityFlagCap = (int)grading["IntegrityFlagCap"]!;
                        }

                        if (json["Monitoring"] is JsonObject monitoring)
                        {
                            if (monitoring["HeadYawThreshold"] != null) HeadYawThreshold = (double)monitoring["HeadYawThreshold"]!;
                            if (monitoring["HeadPitchThreshold"] != null) HeadPitchThreshold = (double)monitoring["HeadPitchThreshold"]!;
                            if (monitoring["NoFaceFrameThreshold"] != null) NoFaceFrameThreshold = (int)monitoring["NoFaceFrameThreshold"]!;
                            if (monitoring["WiggleRoomFrames"] != null) WiggleRoomFrames = (int)monitoring["WiggleRoomFrames"]!;
                            if (monitoring["AlertCooldownSeconds"] != null) AlertCooldownSeconds = (int)monitoring["AlertCooldownSeconds"]!;
                            if (monitoring["ScoreDecayRate"] != null) ScoreDecayRate = (double)monitoring["ScoreDecayRate"]!;
                        }

                        if (json["Gemini"] is JsonObject gemini)
                        {
                            if (gemini["Model"] != null)
                                GeminiModel = NormalizeModelName(gemini["Model"]!.ToString());
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default thresholds if file read/parse fails
            }
        }

        // ─── Gemini Key DPAPI & Model Normalization ─────────────────────────

        public static string NormalizeModelName(string? model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return "gemini-2.5-flash-lite";

            string cleanModelName = model.Trim();
            while (cleanModelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            {
                cleanModelName = cleanModelName.Substring("models/".Length).TrimStart('/');
            }
            return string.IsNullOrWhiteSpace(cleanModelName) ? "gemini-2.5-flash-lite" : cleanModelName;
        }

        public static string SanitizeModelName(string? model) => NormalizeModelName(model);

        public static string GetGeminiApiKey()
        {
            try
            {
                if (File.Exists(KeyFilePath))
                {
                    byte[] encryptedBytes = File.ReadAllBytes(KeyFilePath);
                    byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch
            {
                // Key corrupted or unreadable with current user scope
            }

            return App.Configuration?["Gemini:ApiKey"] ?? string.Empty;
        }

        public static void SaveGeminiApiKey(string plainKey)
        {
            if (string.IsNullOrWhiteSpace(plainKey))
                throw new ArgumentException("API key cannot be empty.", nameof(plainKey));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainKey.Trim());
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFilePath, encryptedBytes);

            if (App.Configuration != null)
            {
                App.Configuration["Gemini:ApiKey"] = plainKey.Trim();
            }
        }

        public static string GetGeminiModel()
        {
            if (!string.IsNullOrWhiteSpace(GeminiModel))
                return NormalizeModelName(GeminiModel);

            return NormalizeModelName(App.Configuration?["Gemini:Model"]);
        }

        public static void SaveGeminiModel(string model)
        {
            GeminiModel = NormalizeModelName(model);

            if (App.Configuration != null)
            {
                App.Configuration["Gemini:Model"] = GeminiModel;
            }

            try
            {
                JsonObject root = new JsonObject();
                if (File.Exists(SettingsFilePath))
                {
                    string content = File.ReadAllText(SettingsFilePath);
                    root = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
                }

                JsonObject geminiNode = root["Gemini"] as JsonObject ?? new JsonObject();
                geminiNode["Model"] = GeminiModel;
                root["Gemini"] = geminiNode;

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(SettingsFilePath, root.ToJsonString(options));
            }
            catch
            {
                // Fallback: in-memory update succeeded
            }
        }

        public static async Task<(bool Success, string Message)> TestGeminiConnectionAsync(string apiKey, string? model = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return (false, "Please provide an API key to test.");

            string activeModel = NormalizeModelName(!string.IsNullOrWhiteSpace(model) ? model : GetGeminiModel());

            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{activeModel}:generateContent?key={apiKey.Trim()}";
                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = "ping" } } }
                    }
                };

                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, $"Connection successful! Model '{activeModel}' verified.");
                }

                // Fallback: check if API key is valid via models list endpoint
                using var listResponse = await _httpClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey.Trim()}");
                if (listResponse.IsSuccessStatusCode)
                {
                    return (true, $"API key is valid, but '{activeModel}' returned {response.StatusCode}. Try selecting another model.");
                }

                string errorBody = await response.Content.ReadAsStringAsync();
                return (false, $"Gemini API error ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}");
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}");
            }
        }

        // ─── Grading Thresholds Persistence ─────────────────────────────────

        public static void SaveGradingCutoffs(decimal passing, decimal excellent, decimal good, decimal fair, int flagCap)
        {
            if (passing < 0 || passing > 100 ||
                excellent < 0 || excellent > 100 ||
                good < 0 || good > 100 ||
                fair < 0 || fair > 100)
            {
                throw new ArgumentException("Grade cutoffs must be between 0% and 100%.");
            }

            if (!(excellent > good && good > fair && fair >= passing))
            {
                throw new ArgumentException("Thresholds must follow descending order: Excellent > Good > Fair >= Passing.");
            }

            if (flagCap <= 0)
            {
                throw new ArgumentException("Integrity flag cap must be greater than 0.");
            }

            PassingGrade = passing;
            ExcellentThreshold = excellent;
            GoodThreshold = good;
            FairThreshold = fair;
            IntegrityFlagCap = flagCap;

            UpdateJson(json =>
            {
                var grading = json["Grading"] as JsonObject ?? new JsonObject();
                grading["PassingGrade"] = passing;
                grading["ExcellentThreshold"] = excellent;
                grading["GoodThreshold"] = good;
                grading["FairThreshold"] = fair;
                grading["IntegrityFlagCap"] = flagCap;
                json["Grading"] = grading;
            });
        }

        // ─── Monitoring Sensitivity Persistence ─────────────────────────────

        public static void SaveMonitoringSettings(double yaw, double pitch, int noFace, int wiggle, int cooldown, double decay)
        {
            if (yaw < 5 || yaw > 90) throw new ArgumentException("Yaw threshold must be between 5° and 90°.");
            if (pitch < 5 || pitch > 90) throw new ArgumentException("Pitch threshold must be between 5° and 90°.");
            if (noFace < 5 || noFace > 300) throw new ArgumentException("No-face frames must be between 5 and 300.");
            if (wiggle < 5 || wiggle > 300) throw new ArgumentException("Wiggle room frames must be between 5 and 300.");
            if (cooldown < 1 || cooldown > 120) throw new ArgumentException("Alert cooldown must be between 1 and 120 seconds.");
            if (decay <= 0 || decay > 1.0) throw new ArgumentException("Score decay rate must be between 0.001 and 1.0.");

            HeadYawThreshold = yaw;
            HeadPitchThreshold = pitch;
            NoFaceFrameThreshold = noFace;
            WiggleRoomFrames = wiggle;
            AlertCooldownSeconds = cooldown;
            ScoreDecayRate = decay;

            UpdateJson(json =>
            {
                var monitoring = json["Monitoring"] as JsonObject ?? new JsonObject();
                monitoring["HeadYawThreshold"] = yaw;
                monitoring["HeadPitchThreshold"] = pitch;
                monitoring["NoFaceFrameThreshold"] = noFace;
                monitoring["WiggleRoomFrames"] = wiggle;
                monitoring["AlertCooldownSeconds"] = cooldown;
                monitoring["ScoreDecayRate"] = decay;
                json["Monitoring"] = monitoring;
            });
        }

        public static void ResetMonitoringDefaults()
        {
            SaveMonitoringSettings(25.0, 20.0, 45, 60, 10, 0.02);
        }

        private static void UpdateJson(Action<JsonObject> modify)
        {
            JsonObject json;
            if (File.Exists(SettingsFilePath))
            {
                string content = File.ReadAllText(SettingsFilePath);
                json = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
            }
            else
            {
                json = new JsonObject();
            }

            modify(json);

            File.WriteAllText(SettingsFilePath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            if (App.Configuration is Microsoft.Extensions.Configuration.IConfigurationRoot root)
            {
                root.Reload();
            }
        }
    }
}

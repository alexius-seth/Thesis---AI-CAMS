using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ACPAMS.Services
{
    public class ConnectionParameters
    {
        public string Server { get; set; } = "localhost\\SQLEXPRESS";
        public string Database { get; set; } = "aicams";
        public bool IsWindowsAuth { get; set; } = true;
        public string Username { get; set; } = "sa";
        public string Password { get; set; } = string.Empty;
        public bool TrustServerCertificate { get; set; } = true;
        public bool IsStudentClient { get; set; } = false;
    }

    public class ConnectionTestResult
    {
        public bool Success { get; set; }
        public int TableCount { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TroubleshootingHint { get; set; } = string.Empty;
        public bool CanAutoCreateDatabase { get; set; }
    }

    public static class ConfigurationService
    {
        private static readonly string BaseSettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        private static readonly string UserSettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AICAMS",
            "appsettings.user.json"
        );

        public static string GetCurrentConnectionString()
        {
            string? connStr = App.Configuration?.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connStr))
                return connStr;

            if (File.Exists(BaseSettingsFilePath))
            {
                try
                {
                    string text = File.ReadAllText(BaseSettingsFilePath);
                    var node = JsonNode.Parse(text);
                    string? cs = node?["ConnectionStrings"]?["DefaultConnection"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(cs))
                        return cs;
                }
                catch { }
            }

            return "Server=(localdb)\\mssqllocaldb;Database=aicams;Trusted_Connection=True;TrustServerCertificate=True;";
        }

        public static ConnectionParameters ParseConnectionString(string? connStr)
        {
            var p = new ConnectionParameters();
            if (string.IsNullOrWhiteSpace(connStr))
                return p;

            try
            {
                var builder = new SqlConnectionStringBuilder(connStr);
                p.Server = builder.DataSource;
                p.Database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "aicams" : builder.InitialCatalog;
                p.IsWindowsAuth = builder.IntegratedSecurity;
                p.Username = string.IsNullOrWhiteSpace(builder.UserID) ? "sa" : builder.UserID;
                p.Password = builder.Password;
                p.TrustServerCertificate = builder.TrustServerCertificate;

                bool isLocal = p.Server.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                               p.Server.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase) ||
                               p.Server.StartsWith(".\\") ||
                               p.Server.Equals(".") ||
                               p.Server.Contains("(localdb)", StringComparison.OrdinalIgnoreCase);
                p.IsStudentClient = !isLocal;
            }
            catch
            {
                // Fall back to defaults on parsing error
            }

            return p;
        }

        public static string BuildConnectionString(ConnectionParameters p, int timeoutSeconds = 15)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = string.IsNullOrWhiteSpace(p.Server) ? "localhost\\SQLEXPRESS" : p.Server.Trim(),
                InitialCatalog = string.IsNullOrWhiteSpace(p.Database) ? "aicams" : p.Database.Trim(),
                TrustServerCertificate = p.TrustServerCertificate,
                ConnectTimeout = timeoutSeconds
            };

            if (p.IsWindowsAuth)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = string.IsNullOrWhiteSpace(p.Username) ? "sa" : p.Username.Trim();
                builder.Password = p.Password ?? string.Empty;
            }

            return builder.ConnectionString;
        }

        public static async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, int timeoutSeconds = 5)
        {
            var result = new ConnectionTestResult();
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = timeoutSeconds
                };
                result.DatabaseName = builder.InitialCatalog;

                using var conn = new SqlConnection(builder.ConnectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0;";
                cmd.CommandTimeout = timeoutSeconds;
                var countObj = await cmd.ExecuteScalarAsync();
                result.TableCount = Convert.ToInt32(countObj);

                result.Success = true;
                result.Message = $"Connection Successful! Found database '{result.DatabaseName}' with {result.TableCount} tables.";
            }
            catch (SqlException ex)
            {
                result.Success = false;
                result.Message = $"Connection failed (Error {ex.Number}): {ex.Message}";

                if (ex.Number == 26 || ex.Number == 53 || ex.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase))
                {
                    result.TroubleshootingHint = "Cannot reach SQL Server host. Verify the server name/IP, ensure SQL Server is running, TCP/IP is enabled in SQL Server Configuration Manager, and Windows Firewall allows inbound TCP port 1433.";
                }
                else if (ex.Number == 18456 || ex.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase))
                {
                    result.TroubleshootingHint = "Authentication failed. Check your username and password. If using SQL Server Authentication, ensure the server is configured for Mixed Mode Authentication (SQL Server and Windows Authentication).";
                }
                else if (ex.Number == 4060 || ex.Message.Contains("Cannot open database", StringComparison.OrdinalIgnoreCase))
                {
                    result.CanAutoCreateDatabase = true;
                    result.Success = true; // Server is accessible, database will be created on migration!
                    result.Message = $"Server connection successful! Database '{result.DatabaseName}' will be created automatically upon launch.";
                    result.TroubleshootingHint = "Database does not exist yet, but the server is reachable and will initialize the database during launch.";
                }
                else if (ex.Message.Contains("LocalDB", StringComparison.OrdinalIgnoreCase))
                {
                    result.TroubleshootingHint = "LocalDB instance registry error or instance not running. Run 'sqllocaldb start mssqllocaldb' in PowerShell, or switch Server to a standard SQL Server instance (e.g. localhost\\SQLEXPRESS).";
                }
                else
                {
                    result.TroubleshootingHint = "Check host availability, network connection, and server instance configuration.";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Connection failed: {ex.Message}";
                result.TroubleshootingHint = "Check connection parameters and network availability.";
            }

            return result;
        }

        public static async Task<ConnectionTestResult> TestCurrentConnectionAsync(int timeoutSeconds = 3)
        {
            string current = GetCurrentConnectionString();
            return await TestConnectionAsync(current, timeoutSeconds);
        }

        public static void SaveConnectionString(string connectionString)
        {
            if (App.Configuration != null)
            {
                App.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
            }

            UpdateJsonFile(BaseSettingsFilePath, connectionString);

            if (File.Exists(UserSettingsFilePath))
            {
                UpdateJsonFile(UserSettingsFilePath, connectionString);
            }
        }

        public static bool ArePoliciesAccepted()
        {
            string? accepted = App.Configuration?["Policies:Accepted"];
            if (bool.TryParse(accepted, out bool isAccepted) && isAccepted)
                return true;

            if (File.Exists(UserSettingsFilePath))
            {
                try
                {
                    string text = File.ReadAllText(UserSettingsFilePath);
                    var node = JsonNode.Parse(text);
                    if (node?["Policies"]?["Accepted"]?.GetValue<bool>() == true)
                        return true;
                }
                catch { }
            }

            if (File.Exists(BaseSettingsFilePath))
            {
                try
                {
                    string text = File.ReadAllText(BaseSettingsFilePath);
                    var node = JsonNode.Parse(text);
                    if (node?["Policies"]?["Accepted"]?.GetValue<bool>() == true)
                        return true;
                }
                catch { }
            }

            return false;
        }

        public static void SavePoliciesAccepted(bool accepted)
        {
            string timestamp = DateTime.UtcNow.ToString("o");
            if (App.Configuration != null)
            {
                App.Configuration["Policies:Accepted"] = accepted.ToString().ToLowerInvariant();
                App.Configuration["Policies:AcceptedAt"] = timestamp;
            }

            UpdatePoliciesInFile(BaseSettingsFilePath, accepted, timestamp);

            if (File.Exists(UserSettingsFilePath))
            {
                UpdatePoliciesInFile(UserSettingsFilePath, accepted, timestamp);
            }
        }

        private static void UpdatePoliciesInFile(string filePath, bool accepted, string timestamp)
        {
            try
            {
                JsonObject root;
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    root = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                if (root["Policies"] is not JsonObject policiesObj)
                {
                    policiesObj = new JsonObject();
                    root["Policies"] = policiesObj;
                }

                policiesObj["Accepted"] = accepted;
                policiesObj["AcceptedAt"] = timestamp;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJson = root.ToJsonString(options);

                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save policies to {filePath}: {ex.Message}");
            }
        }

        private static void UpdateJsonFile(string filePath, string connectionString)
        {
            try
            {
                JsonObject root;
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    root = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                if (root["ConnectionStrings"] is not JsonObject connStrings)
                {
                    connStrings = new JsonObject();
                    root["ConnectionStrings"] = connStrings;
                }

                connStrings["DefaultConnection"] = connectionString;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJson = root.ToJsonString(options);

                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save connection string to {filePath}: {ex.Message}");
            }
        }
    }
}


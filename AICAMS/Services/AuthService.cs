using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService()
        {
            _context = new AppDbContext();
        }

        public async Task<(User? User, string? ErrorMessage)> LoginWithStatusAsync(string usernameOrStudentId, string password)
        {
            var user = await _context.Users
                .Include(u => u.Student)
                .FirstOrDefaultAsync(u => u.Username == usernameOrStudentId);

            if (user == null) return (null, "Invalid username or password. Please try again.");

            // Verify password
            bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!isValid) return (null, "Invalid username or password. Please try again.");

            if (!user.IsActive || (user.Role == "student" && user.Student != null && !user.Student.IsActive))
            {
                return (null, "This account has been deactivated. Contact your department administrator.");
            }

            return (user, null);
        }

        public async Task<User?> LoginAsync(string usernameOrStudentId, string password)
        {
            var (user, _) = await LoginWithStatusAsync(usernameOrStudentId, password);
            return user;
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool ValidatePassword(string password)
        {
            // Minimum 8 characters, must have letter, number, and symbol
            if (password.Length < 8) return false;
            bool hasLetter = password.Any(char.IsLetter);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));
            return hasLetter && hasDigit && hasSymbol;
        }

        public static string ComputeSha256Hash(string raw)
        {
            byte[] bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw.Trim()));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static bool VerifyFacultyRegistrationCode(string? submittedCode)
        {
            if (string.IsNullOrWhiteSpace(submittedCode))
                return false;

            string submittedHash = ComputeSha256Hash(submittedCode);

            // 1. Environment variable override
            string? envCode = Environment.GetEnvironmentVariable("ACPAMS_FACULTY_REGISTRATION_CODE");
            if (!string.IsNullOrWhiteSpace(envCode))
            {
                string envHash = ComputeSha256Hash(envCode);
                return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(submittedHash),
                    System.Text.Encoding.UTF8.GetBytes(envHash));
            }

            // 2. Hashed setting in app configuration (appsettings.json / secrets)
            string? storedHash = App.Configuration?["Faculty:RegistrationCodeHash"];
            if (!string.IsNullOrWhiteSpace(storedHash))
            {
                return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(submittedHash),
                    System.Text.Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant()));
            }

            // 3. Fallback plaintext setting in app configuration
            string? configCode = App.Configuration?["Faculty:RegistrationCode"];
            if (!string.IsNullOrWhiteSpace(configCode))
            {
                string configHash = ComputeSha256Hash(configCode);
                return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(submittedHash),
                    System.Text.Encoding.UTF8.GetBytes(configHash));
            }

            // 4. Default fallback secret hash
            string defaultHash = ComputeSha256Hash("FACULTY-2026-SECRET");
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(submittedHash),
                System.Text.Encoding.UTF8.GetBytes(defaultHash));
        }

        public static void UpdateFacultyRegistrationCode(string newCode)
        {
            if (string.IsNullOrWhiteSpace(newCode))
                throw new ArgumentException("Registration code cannot be empty.", nameof(newCode));

            string hash = ComputeSha256Hash(newCode);

            // Persist to appsettings.json
            string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            System.Text.Json.Nodes.JsonObject json;
            if (System.IO.File.Exists(filePath))
            {
                string content = System.IO.File.ReadAllText(filePath);
                json = System.Text.Json.Nodes.JsonNode.Parse(content) as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
            }
            else
            {
                json = new System.Text.Json.Nodes.JsonObject();
            }

            if (!json.ContainsKey("Faculty") || json["Faculty"] is not System.Text.Json.Nodes.JsonObject)
            {
                json["Faculty"] = new System.Text.Json.Nodes.JsonObject();
            }

            var facultyNode = json["Faculty"]!.AsObject();
            facultyNode["RegistrationCodeHash"] = hash;
            facultyNode.Remove("RegistrationCode");

            System.IO.File.WriteAllText(filePath, json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            // Refresh runtime configuration
            if (App.Configuration is Microsoft.Extensions.Configuration.IConfigurationRoot root)
            {
                root.Reload();
            }
            App.Configuration["Faculty:RegistrationCodeHash"] = hash;
            App.Configuration["Faculty:RegistrationCode"] = null;
        }

        public async Task<(bool Success, string? ErrorMessage)> RegisterFacultyAsync(
            string firstName,
            string? middleInitial,
            string lastName,
            string username,
            string password,
            string registrationCode)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(registrationCode))
            {
                return (false, "Please fill in all required fields.");
            }

            if (!VerifyFacultyRegistrationCode(registrationCode))
            {
                return (false, "Invalid registration code.");
            }

            if (!ValidatePassword(password))
            {
                return (false, "Password must be at least 8 characters and include letters, numbers, and symbols.");
            }

            string cleanUsername = username.Trim();
            bool exists = await _context.Users.AnyAsync(u => u.Username == cleanUsername);
            if (exists)
            {
                return (false, "A user with this username already exists.");
            }

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Role = "instructor",
                Username = cleanUsername,
                PasswordHash = HashPassword(password),
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var instructor = new Instructor
            {
                InstructorId = $"INST-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                UserId = user.UserId,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                MiddleInitial = string.IsNullOrWhiteSpace(middleInitial) ? null : middleInitial.Trim(),
                Department = "Department of Computer Studies",
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            _context.Instructors.Add(instructor);
            await _context.SaveChangesAsync();

            return (true, null);
        }
    }
}
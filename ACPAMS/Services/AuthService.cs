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

        public async Task<User?> LoginAsync(string usernameOrStudentId, string password)
        {
            // Find user by username (faculty) or student ID (student)
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == usernameOrStudentId
                                       && u.IsActive);

            if (user == null) return null;

            // Verify password
            bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!isValid) return null;

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
    }
}
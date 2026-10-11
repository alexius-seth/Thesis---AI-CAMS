using ACPAMS.Data;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ACPAMS.Services
{
    public class StudentService
    {
        private readonly AppDbContext? _context;

        public StudentService()
        {
        }

        public StudentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasExamAttemptsAsync(string studentId)
        {
            using var context = _context ?? new AppDbContext();
            return await context.StudentExamAttempts.AnyAsync(a => a.StudentId == studentId);
        }

        public async Task<string> DeleteOrDeactivateStudentAsync(string studentId)
        {
            using var context = _context ?? new AppDbContext();
            var student = await context.Students
                .Include(s => s.ExamAttempts)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null) return "Student not found.";

            // Case A: Student has never taken an exam -> Safe to permanently delete
            if (student.ExamAttempts == null || !student.ExamAttempts.Any())
            {
                var user = student.User;
                context.Students.Remove(student);
                if (user != null)
                {
                    context.Users.Remove(user);
                }
                await context.SaveChangesAsync();
                return "Student record permanently removed.";
            }

            // Case B: Student has historical exam records -> Soft delete to preserve audit trail
            student.IsActive = false;
            if (student.User != null)
            {
                student.User.IsActive = false;
            }
            await context.SaveChangesAsync();
            return "Student has completed exams on record. Account was deactivated to preserve historical grades.";
        }

        public Task<string> DeleteOrDeactivateStudentAsync(int studentId) =>
            DeleteOrDeactivateStudentAsync(studentId.ToString());

        public async Task<string> ReactivateStudentAsync(string studentId)
        {
            using var context = _context ?? new AppDbContext();
            var student = await context.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null) return "Student not found.";

            student.IsActive = true;
            if (student.User != null)
            {
                student.User.IsActive = true;
            }
            await context.SaveChangesAsync();
            return "Student account reactivated successfully.";
        }

        public Task<string> ReactivateStudentAsync(int studentId) =>
            ReactivateStudentAsync(studentId.ToString());

        public async Task<(bool Success, string Message, string? TempPassword)> ResetPasswordAsync(string studentId)
        {
            using var context = _context ?? new AppDbContext();
            var student = await context.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                return (false, "Student not found.", null);

            string tempPassword = $"Temp@{student.StudentId}";
            string hash = BCrypt.Net.BCrypt.HashPassword(tempPassword, 12);

            student.MustChangePassword = true;

            if (student.User != null)
            {
                student.User.PasswordHash = hash;
            }
            else
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Username == student.StudentId);
                if (user != null)
                {
                    user.PasswordHash = hash;
                }
            }

            await context.SaveChangesAsync();
            return (true, $"Password reset successfully.\n\nTemporary Credentials:\nUsername: {student.StudentId}\nPassword: {tempPassword}", tempPassword);
        }
    }
}


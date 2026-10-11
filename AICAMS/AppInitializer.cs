using System;
using System.Linq;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data; 

namespace ACPAMS
{
    public static class AppInitializer
    {
        private static AppDbContext _dbContext;
        private static string _currentInstructorId;
        private static bool _isInitialized = false;

        public const string DefaultPassword = "Admin@1234";

        public static bool IsDefaultPassword(string passwordHash)
        {
            if (string.IsNullOrEmpty(passwordHash)) return false;
            try
            {
                return BCrypt.Net.BCrypt.Verify(DefaultPassword, passwordHash);
            }
            catch
            {
                return false;
            }
        }

        public static void Initialize(AppDbContext dbContext)
        {
            _dbContext = dbContext;

            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                // Ensure database is created
                _dbContext.Database.Migrate();

                // Self-healing schema check for questions.section_name
                _dbContext.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('questions') AND name = 'section_name'
                    )
                    BEGIN
                        ALTER TABLE questions ADD section_name NVARCHAR(MAX) NULL;
                    END

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('student_exam_attempts') AND name = 'extra_time_minutes'
                    )
                    BEGIN
                        ALTER TABLE student_exam_attempts ADD extra_time_minutes INT NOT NULL DEFAULT 0;
                    END

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('audit_logs') AND name = 'is_dismissed'
                    )
                    BEGIN
                        ALTER TABLE audit_logs ADD is_dismissed BIT NOT NULL DEFAULT 0;
                    END

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('audit_logs') AND name = 'dismissal_reason'
                    )
                    BEGIN
                        ALTER TABLE audit_logs ADD dismissal_reason NVARCHAR(255) NULL;
                    END

                    IF EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('students') AND name = 'program' AND max_length < 200 AND max_length > 0
                    )
                    BEGIN
                        ALTER TABLE students ALTER COLUMN program NVARCHAR(100) NULL;
                    END

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('dbo.students') AND name = 'is_active'
                    )
                    BEGIN
                        ALTER TABLE dbo.students ADD is_active BIT NOT NULL DEFAULT 1;
                    END

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('dbo.students') AND name = 'must_change_password'
                    )
                    BEGIN
                        ALTER TABLE dbo.students ADD must_change_password BIT NOT NULL DEFAULT 0;
                    END");

                // Initialize default data
                InitializeDefaultData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
                throw;
            }
        }

        private static void InitializeDefaultData()
        {
            if (_dbContext == null) return;

            try
            {
                // Check if any instructors exist
                if (!_dbContext.Instructors.Any())
                {
                    var defaultInstructor = new Instructor
                    {
                        InstructorId = "INST-001",
                        UserId = Guid.NewGuid(),
                        FirstName = "Default",
                        LastName = "Instructor",
                        Department = "Department of Computer Studies",
                        CreatedAt = DateTime.Now
                    };

                    var defaultUser = new User
                    {
                        UserId = defaultInstructor.UserId,
                        Role = "instructor",
                        Username = "instructor",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234"),
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    _dbContext.Users.Add(defaultUser);
                    _dbContext.Instructors.Add(defaultInstructor);
                    _dbContext.SaveChanges();

                    System.Diagnostics.Debug.WriteLine("Default instructor created successfully.");
                }

                // Get the first instructor
                var instructor = _dbContext.Instructors.FirstOrDefault();
                if (instructor != null)
                {
                    _currentInstructorId = instructor.InstructorId;
                    System.Diagnostics.Debug.WriteLine($"Current instructor ID: {_currentInstructorId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing default data: {ex.Message}");
                throw;
            }
        }

        public static AppDbContext GetDbContext()
        {
            return _dbContext;
        }

        public static string GetCurrentInstructorId()
        {
            if (string.IsNullOrEmpty(_currentInstructorId) && _dbContext != null)
            {
                try
                {
                    var instructor = _dbContext.Instructors.FirstOrDefault();
                    if (instructor != null)
                    {
                        _currentInstructorId = instructor.InstructorId;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting instructor ID: {ex.Message}");
                }
            }
            return _currentInstructorId;
        }
    }
}
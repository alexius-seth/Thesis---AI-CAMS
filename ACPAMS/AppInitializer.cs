using System;
using System.Linq;
using ACPAMS.Models;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS
{
    public static class AppInitializer
    {
        private static AppDbContext _dbContext;
        private static string _currentInstructorId;
        private static bool _isInitialized = false;

        public static void Initialize(AppDbContext dbContext)
        {
            _dbContext = dbContext;

            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                // Ensure database is created
                _dbContext.Database.Migrate();

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
                        Department = "Computer Studies",
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
using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace ACPAMS
{
    public partial class App : Application
    {
        private static AppDbContext _dbContext;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                _dbContext = new AppDbContext();

                // Test the connection
                var canConnect = _dbContext.Database.CanConnect();
                System.Diagnostics.Debug.WriteLine($"Database connection: {(canConnect ? "SUCCESS" : "FAILED")}");

                if (canConnect)
                {
                    AppInitializer.Initialize(_dbContext);
                }
                else
                {
                    MessageBox.Show("Cannot connect to the database. Please make sure MySQL is running.",
                        "Database Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Application startup error: {ex.Message}");
                MessageBox.Show($"Database connection error: {ex.Message}\n\nPlease make sure MySQL is running and the database 'acpams_db' exists.",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _dbContext?.Dispose();
            base.OnExit(e);
        }

        public static AppDbContext GetDbContext() => _dbContext;
        public static string GetCurrentInstructorId() => AppInitializer.GetCurrentInstructorId();
    }
}
using System.Windows;

namespace ACPAMS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Run EF Core migrations and seed default instructor
                using var context = new ACPAMS.Data.AppDbContext();
                AppInitializer.Initialize(context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Application startup error: {ex.Message}");
                MessageBox.Show(
                    $"Database initialization error:\n\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
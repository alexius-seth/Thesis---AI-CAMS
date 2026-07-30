using System.Windows;
using Microsoft.Extensions.Configuration;

namespace ACPAMS
{
    public partial class App : Application
    {
        // Exposed so any service (e.g. a future GeminiGradingService) can read
        // configuration values without each one re-building its own ConfigurationBuilder.
        public static IConfiguration Configuration { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Configuration = new ConfigurationBuilder()
                // Reads from %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json
                // Never touches anything inside the repo folder.
                .AddUserSecrets<App>()
                .Build();
            MessageBox.Show(Configuration["Gemini:ApiKey"] ?? "(NULL - not found!)");

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
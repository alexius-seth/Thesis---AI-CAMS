using System.Windows;
using Microsoft.Extensions.Configuration;
using ACPAMS.Services;

namespace ACPAMS
{
    public partial class App : Application
    {
        // Exposed so any service (e.g. GeminiRubricGradingService) can read
        // configuration values without each one re-building its own ConfigurationBuilder.
        public static IConfiguration Configuration { get; private set; } = null!;

        // Polls the AiJob queue in the background for the app's whole lifetime.
        // See AiJobProcessorService for why every app instance (each lab PC)
        // running its own copy of this is fine.
        private static AiJobProcessorService? _aiJobProcessor;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Configuration = new ConfigurationBuilder()
                // Reads from %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json
                // Never touches anything inside the repo folder.
                .AddUserSecrets<App>()
                .Build();

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
                return;
            }

            // Check every 15s for queued AI grading jobs. Adjust if Gemini calls
            // are consistently slower/faster than expected once tested for real.
            _aiJobProcessor = new AiJobProcessorService(TimeSpan.FromSeconds(15));
            _aiJobProcessor.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _aiJobProcessor?.Stop();
            base.OnExit(e);
        }
    }
}
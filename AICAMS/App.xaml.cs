using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ACPAMS.Services;
using ACPAMS.Views;

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

        public static void StartAiJobProcessor()
        {
            if (_aiJobProcessor == null)
            {
                _aiJobProcessor = new AiJobProcessorService(TimeSpan.FromSeconds(15));
                _aiJobProcessor.Start();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception traps to prevent silent exits
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show($"Fatal Startup Exception:\n{args.ExceptionObject}", "AI-CAMS Boot Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"UI Thread Exception:\n{args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}", "AI-CAMS Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            string userAppSettingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AICAMS",
                "appsettings.user.json"
            );

            try
            {
                Configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile(userAppSettingsPath, optional: true, reloadOnChange: true)
                    .AddUserSecrets<App>(optional: true)
                    .Build();

                AppSettingsService.Initialize();

                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
                QuestPDF.Settings.UseSystemFonts = true;
                QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Configuration initialization error: {ex.Message}");
            }

            // Shielded database connectivity verification
            bool canConnect = false;
            try
            {
                using (var db = new ACPAMS.Data.AppDbContext())
                {
                    db.Database.SetCommandTimeout(3);
                    canConnect = db.Database.CanConnect();
                }
            }
            catch (Exception)
            {
                // Suppress driver/registry errors (e.g. SQLLocalDB registry fault, offline host, timeout)
                canConnect = false;
            }

            // Shielded policy compliance check
            bool policiesAccepted = false;
            try
            {
                policiesAccepted = ConfigurationService.ArePoliciesAccepted();
            }
            catch
            {
                policiesAccepted = false;
            }

            if (!canConnect || !policiesAccepted)
            {
                var wizard = new SetupWizardWindow();
                MainWindow = wizard;
                wizard.Show();
            }
            else
            {
                try
                {
                    // Run EF Core migrations and self-healing schema initialization
                    using var context = new ACPAMS.Data.AppDbContext();
                    AppInitializer.Initialize(context);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Initial database setup failed: {ex.Message}");
                    var wizard = new SetupWizardWindow();
                    MainWindow = wizard;
                    wizard.Show();
                    return;
                }

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();

                StartAiJobProcessor();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _aiJobProcessor?.Stop();
            base.OnExit(e);
        }
    }
}
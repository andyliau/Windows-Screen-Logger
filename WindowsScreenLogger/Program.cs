using System.CommandLine;
using System.Diagnostics;
using WindowsScreenLogger.Installation;

namespace WindowsScreenLogger
{
	internal static class Program
	{
		private static Mutex? mutex = null;
		private static AppConfiguration? appConfig = null;

		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			// If we have command line arguments, use the enhanced command line parser
			if (args.Length > 0)
			{
				var rootCommand = CommandLineHandler.CreateRootCommand();
				rootCommand.Invoke(args);
				return;
			}

			// No arguments - start normally
			StartNormalApplication();
		}

		/// <summary>
		/// Starts the normal application flow
		/// </summary>
		public static void StartNormalApplication(bool noInstallPrompt = false, string? configPath = null)
		{
			try
			{
				// Load configuration
				appConfig = AppConfiguration.Load(configPath);
				appConfig.Validate();

				// Initialize logging
				var logLevel = Enum.TryParse<AppLogger.LogLevel>(appConfig.LogLevel, out var level) 
					? level : AppLogger.LogLevel.Information;
				AppLogger.Initialize(appConfig.EnableLogging, logLevel);
				AppLogger.LogStartup();

				// Clean up old logs
				AppLogger.CleanupOldLogs();

				const string mutexName = "WindowsScreenLoggerMutex";

				// Ensure only one instance is running
				bool createdNew;
				mutex = new Mutex(true, mutexName, out createdNew);

				if (!createdNew)
				{
					AppLogger.LogWarning("Another instance is already running");
					MessageBox.Show("Another instance of the application is already running.", 
						"Instance Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				AppLogger.LogInformation("Application instance created successfully");

				// Check if running from install location and prompt for installation if needed
				if (!noInstallPrompt && !SelfInstaller.IsRunningFromInstallLocation() && !SelfInstaller.IsInstalled())
				{
					AppLogger.LogInformation("Application not installed, prompting for installation");
					SelfInstaller.PromptForInstallation();
					// If user declined installation, continue running from current location
				}

				// Set the process priority to BelowNormal
				Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
				AppLogger.LogDebug("Process priority set to BelowNormal");

				// Initialize application configuration
				ApplicationConfiguration.Initialize();
				AppLogger.LogInformation("Windows Forms application configuration initialized");

				// Migrate legacy settings if needed
				if (ShouldMigrateLegacySettings())
				{
					AppLogger.LogInformation("Migrating legacy settings");
					appConfig.MigrateFromLegacySettings();
				}

				// Start the main form
				AppLogger.LogInformation("Starting main application form");
				Application.Run(new MainForm(appConfig));

				AppLogger.LogShutdown();
			}
			catch (Exception ex)
			{
				AppLogger.LogException(ex, "Application startup");
				MessageBox.Show($"Fatal error during application startup: {ex.Message}", 
					"Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Environment.Exit(1);
			}
			finally
			{
				// Release the mutex when the application exits
				GC.KeepAlive(mutex);
				mutex?.Dispose();
			}
		}

		private static bool ShouldMigrateLegacySettings()
		{
			// Check if legacy settings exist and new config doesn't
			try
			{
				return !File.Exists(AppConfiguration.DefaultConfigPath) && 
					   Settings.Default.CaptureInterval > 0; // Indicates legacy settings exist
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the current application configuration
		/// </summary>
		public static AppConfiguration? GetConfiguration() => appConfig;
	}
}
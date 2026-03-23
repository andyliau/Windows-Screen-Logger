using System.CommandLine;
using System.Diagnostics;
using WindowsActivityLogger.Installation;

namespace WindowsActivityLogger
{
	internal static class Program
	{
		private static Mutex? mutex = null;
		private static AppConfiguration? appConfig = null;
		private static ILogger logger = new DefaultLogger();

		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			// Write startup trace to %TEMP%\WAL_startup.log before anything else.
			// This always works regardless of config, for diagnosing launch failures.
			WriteStartupTrace(args, "Main entered");

			// Bootstrap logging before config is loaded
			logger.Initialize(true, "Debug");
			logger.LogCommandLineArgs(args);

			// If we have command line arguments, use the enhanced command line parser
			if (args.Length > 0)
			{
				logger.LogInformation("Processing command line arguments");
				CommandLineHandler.SetLogger(logger);
				
				try
				{
					// First try to handle legacy command formats (e.g., /uninstall, /install)
					if (CommandLineHandler.TryHandleLegacyCommand(args))
					{
						logger.LogInformation("Legacy command format processed successfully");
						return;
					}

					// Use modern System.CommandLine parser
					logger.LogInformation("Processing with System.CommandLine parser");
					var rootCommand = CommandLineHandler.CreateRootCommand();
					var result = rootCommand.Parse(args).Invoke();
					logger.LogInformation($"Command line processing completed with exit code: {result}");
					return;
				}
				catch (Exception ex)
				{
					logger.LogException(ex, "Command line processing");
					Environment.Exit(1);
				}
			}

			// No arguments - start normally
			logger.LogInformation("No command line arguments, starting normal application flow");
			StartNormalApplication();
		}

		/// <summary>
		/// Starts the normal application flow
		/// </summary>
		public static void StartNormalApplication(bool noInstallPrompt = false, string? configPath = null, bool isPostInstall = false)
		{
			try
			{
				// Load configuration
				appConfig = AppConfiguration.Load(configPath);
				appConfig.Validate();

				// Always write to log file so startup and install events are always captured.
				// (The 'enableLogging' config was gating file output; force it true here.)
				logger.Initialize(true, appConfig.LogLevel);
				logger.LogStartup();

				// Clean up old logs
				logger.CleanupOldLogs();

				const string mutexName = "WindowsActivityLoggerMutex";

				// Ensure only one instance is running.
				// AbandonedMutexException means the previous owner exited via Environment.Exit
				// without calling ReleaseMutex(); we still acquire ownership, so treat as success.
				bool createdNew;
				try
				{
					mutex = new Mutex(true, mutexName, out createdNew);
				}
				catch (AbandonedMutexException)
				{
					createdNew = true; // we acquired the abandoned mutex — proceed as owner
				}

				WriteStartupTrace([], $"Mutex: createdNew={createdNew}, isPostInstall={isPostInstall}");
				logger.LogInformation($"Mutex attempt: createdNew={createdNew}, isPostInstall={isPostInstall}");

				if (!createdNew)
				{
					// Handle post-installation startup with enhanced retry logic
					if (isPostInstall || (SelfInstaller.IsRunningFromInstallLocation() && SelfInstaller.IsInstalled()))
					{
						logger.LogInformation("Post-installation startup detected, waiting for previous instance to exit...");
						
						// Multiple retry attempts with increasing delays
						int maxRetries = 5;
						for (int i = 0; i < maxRetries; i++)
						{
							Thread.Sleep(1000 * (i + 1)); // 1s, 2s, 3s, 4s, 5s
							mutex?.Dispose();
							try
							{
								mutex = new Mutex(true, mutexName, out createdNew);
							}
							catch (AbandonedMutexException)
							{
								createdNew = true;
							}
							
							if (createdNew)
							{
								logger.LogInformation($"Successfully acquired mutex after {i + 1} retries");
								break;
							}
							
							logger.LogDebug($"Retry {i + 1}/{maxRetries} failed, previous instance still running");
						}
						
						if (!createdNew)
						{
							logger.LogWarning("Another instance is still running after all retries");
							if (isPostInstall)
							{
								MessageBox.Show("Installation completed successfully!\n\n" +
									"Another instance of the application is currently running.\n" +
									"Please close the existing instance and restart the application manually.", 
									"Installation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
							}
							else
							{
								MessageBox.Show("Another instance of the application is already running.", 
									"Instance Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
							}
							return;
						}
					}
					else
					{
						logger.LogWarning("Another instance is already running");
						MessageBox.Show("Another instance of the application is already running.", 
							"Instance Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
						return;
					}
				}

				logger.LogInformation("Application instance created successfully");

				// Check if running from install location and prompt for installation if needed
				if (!noInstallPrompt && !SelfInstaller.IsRunningFromInstallLocation() && !SelfInstaller.IsInstalled())
				{
					logger.LogInformation("Application not installed, prompting for installation");
					SelfInstaller.PromptForInstallation();
					// If user declined installation, continue running from current location
				}

				// Set the process priority to BelowNormal
				Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
				logger.LogDebug("Process priority set to BelowNormal");

				// Initialize application configuration
				ApplicationConfiguration.Initialize();
				logger.LogInformation("Windows Forms application configuration initialized");

				// Start the main form
				logger.LogInformation("Starting main application form");
				Application.Run(new MainForm(appConfig, logger: logger, postInstall: isPostInstall));

				logger.LogShutdown();
			}
			catch (Exception ex)
			{
				logger.LogException(ex, "Application startup");
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

		/// <summary>
		/// Gets the current application configuration
		/// </summary>
		public static AppConfiguration? GetConfiguration() => appConfig;

		/// <summary>
		/// Writes an entry to %TEMP%\WAL_startup.log regardless of logging config.
		/// First place to look when diagnosing launch failures.
		/// </summary>
		internal static void WriteStartupTrace(string[] args, string message)
		{
			try
			{
				var logPath = Path.Combine(Path.GetTempPath(), "WAL_startup.log");
				var argsStr = args.Length > 0 ? string.Join(" ", args) : "(none)";
				var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] PID={Environment.ProcessId} | args=[{argsStr}] | {message}";
				File.AppendAllText(logPath, entry + Environment.NewLine);
			}
			catch { }
		}

		/// <summary>
		/// Explicitly releases the named-mutex instance lock.
		/// Called by SelfInstaller before launching the installed copy so the new
		/// instance can acquire the mutex on first try (no AbandonedMutexException).
		/// </summary>
		public static void ReleaseInstanceLock()
		{
			if (mutex != null)
			{
				try { mutex.ReleaseMutex(); } catch { }
				mutex.Dispose();
				mutex = null;
			}
		}
	}
}
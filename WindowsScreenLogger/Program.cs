using System.Diagnostics;

namespace WindowsScreenLogger
{
	internal static class Program
	{
		private static Mutex mutex = null;

		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			// Handle command line arguments
			if (args.Length > 0)
			{
				HandleCommandLineArguments(args);
				return;
			}

			const string mutexName = "WindowsScreenLoggerMutex";

			// Ensure only one instance is running
			bool createdNew;
			mutex = new Mutex(true, mutexName, out createdNew);

			if (!createdNew)
			{
				// If another instance is already running, exit the application
				MessageBox.Show("Another instance of the application is already running.", "Instance Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Check if running from install location and prompt for installation if needed
			if (!SelfInstaller.IsRunningFromInstallLocation() && !SelfInstaller.IsInstalled())
			{
				SelfInstaller.PromptForInstallation();
				// If user declined installation, continue running from current location
			}

			// Set the process priority to BelowNormal
			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();
			Application.Run(new MainForm());

			// Release the mutex when the application exits
			GC.KeepAlive(mutex);
		}

		private static void HandleCommandLineArguments(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i].ToLower())
				{
					case "/install":
						// Initialize application for dialogs
						ApplicationConfiguration.Initialize();
						SelfInstaller.PerformInstallation();
						break;

					case "/uninstall":
						// Check for quiet flag
						bool quiet = i + 1 < args.Length && args[i + 1].ToLower() == "/quiet";
						if (!quiet)
						{
							ApplicationConfiguration.Initialize();
						}
						SelfInstaller.PerformUninstallation(quiet);
						break;

					case "/quiet":
						// Handled in uninstall case
						break;

					default:
						MessageBox.Show($"Unknown command line argument: {args[i]}", 
							"Invalid Argument", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						break;
				}
			}
		}
	}
}
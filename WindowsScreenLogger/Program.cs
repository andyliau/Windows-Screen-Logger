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
		static void Main()
		{
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

			// Set the process priority to BelowNormal
			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();
			Application.Run(new MainForm());

			// Release the mutex when the application exits
			GC.KeepAlive(mutex);
		}
	}
}
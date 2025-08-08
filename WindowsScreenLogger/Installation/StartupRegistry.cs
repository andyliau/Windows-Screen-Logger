using Microsoft.Win32;
using System.Diagnostics;

namespace WindowsScreenLogger.Installation
{
    /// <summary>
    /// Manages Windows startup registry entries
    /// </summary>
    public static class StartupRegistry
    {
        private const string AppName = "Windows Screen Logger";
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Enables or disables the application to start with Windows
        /// </summary>
        public static void SetStartupRegistration(bool enable, string executablePath)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                
                if (enable && File.Exists(executablePath))
                {
                    key?.SetValue(AppName, executablePath);
                    Debug.WriteLine($"Startup registration enabled for: {executablePath}");
                }
                else
                {
                    key?.DeleteValue(AppName, false);
                    Debug.WriteLine("Startup registration disabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set startup registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the application is currently registered to start with Windows
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                var value = key?.GetValue(AppName);
                return value != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check startup registration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current startup executable path
        /// </summary>
        public static string? GetStartupExecutablePath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName)?.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get startup executable path: {ex.Message}");
                return null;
            }
        }
    }
}
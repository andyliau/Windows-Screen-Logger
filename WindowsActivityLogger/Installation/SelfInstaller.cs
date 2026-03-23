using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace WindowsActivityLogger.Installation
{
    public static class SelfInstaller
    {
        private const string AppName = "Windows Activity Logger";

        public static string InstallPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        public static string InstalledExecutablePath => Path.Combine(InstallPath, "WindowsActivityLogger.exe");
        public static string InstalledProcessorPath => Path.Combine(InstallPath, "ActivityLogProcessor.exe");

        public static bool IsRunningFromInstallLocation()
        {
            string currentPath = Application.ExecutablePath;
            return string.Equals(currentPath, InstalledExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsInstalled()
        {
            return File.Exists(InstalledExecutablePath);
        }

        public static bool IsElevated()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void PromptForInstallation()
        {
            var result = MessageBox.Show(
                $"Welcome to {AppName}!\n\n" +
                "This application is running from a temporary location. " +
                "Would you like to install it to your system?\n\n" +
                "Installation will:\n" +
                "- Copy the application to your user folder\n" +
                "- Add it to Windows Apps & Features\n" +
                "- Enable proper startup with Windows\n" +
                "- Allow easy uninstallation\n" +
                "- No administrator privileges required\n\n" +
                "The application will automatically restart from the installed location.\n\n" +
                "Install now?",
                "Install Application",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Yes)
            {
                PerformInstallation();
            }
        }

        public static void PerformInstallation()
        {
            try
            {
                // Create installation directory (no admin rights needed for user folder)
                Directory.CreateDirectory(InstallPath);

                // Copy executable
                File.Copy(Application.ExecutablePath, InstalledExecutablePath, true);

                // Extract ActivityLogProcessor alongside main executable
                ExtractEmbeddedBinary("ActivityLogProcessor.exe", InstalledProcessorPath);

                // Remove any leftover traces from the old "Windows Screen Logger" name
                CleanupLegacyInstallation();

                // Register in Windows Apps & Features (user registry only)
                WindowsAppsRegistry.RegisterApplication(InstallPath, InstalledExecutablePath);

                // Set startup registry entry to installed location
                StartupRegistry.SetStartupRegistration(true, InstalledExecutablePath);

                // Confirm success before launching — user click also acts as a natural delay
                // so the new instance rarely needs to retry the mutex.
                MessageBox.Show(
                    $"{AppName} has been installed successfully!\n\n" +
                    "The application will now start and appear in your system tray.\n\n" +
                    "Look for the Activity Logger icon in the notification area (bottom-right of taskbar).",
                    "Installation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Release the mutex explicitly so the new instance can acquire it
                // on its first attempt (avoids AbandonedMutexException from Environment.Exit).
                Program.ReleaseInstanceLock();

                // Start the installed copy, then exit.
                StartInstalledVersion();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed: {ex.Message}",
                    "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Starts the installed executable. The --post-install flag triggers mutex retry logic
        /// in Program.cs so the new instance waits gracefully for this process to exit.
        /// UseShellExecute=true creates the process outside any Windows Job Object that
        /// might otherwise kill child processes when the parent exits.
        /// </summary>
        private static void StartInstalledVersion()
        {
            Program.WriteStartupTrace([], $"Launching installed copy: {InstalledExecutablePath}");
            Process.Start(new ProcessStartInfo(InstalledExecutablePath, "--post-install")
            {
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Removes leftover files and registry entries from the old "Windows Screen Logger" installation.
        /// Safe to call even if nothing exists — all steps are best-effort.
        /// </summary>
        private static void CleanupLegacyInstallation()
        {
            // Old install directory
            var oldInstallPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Windows Screen Logger");
            if (Directory.Exists(oldInstallPath))
            {
                try { Directory.Delete(oldInstallPath, true); }
                catch (Exception ex) { Debug.WriteLine($"Legacy cleanup: could not remove old install dir: {ex.Message}"); }
            }

            // Old AppData folder
            var oldAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowsScreenLogger");
            if (Directory.Exists(oldAppData))
            {
                try { Directory.Delete(oldAppData, true); }
                catch (Exception ex) { Debug.WriteLine($"Legacy cleanup: could not remove old AppData: {ex.Message}"); }
            }

            // Old startup registry values (tried both naming conventions)
            const string runKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, true);
                runKey?.DeleteValue("Windows Screen Logger", false);
                runKey?.DeleteValue("WindowsScreenLogger", false);
            }
            catch (Exception ex) { Debug.WriteLine($"Legacy cleanup: could not remove old startup entry: {ex.Message}"); }

            // Old Windows Apps & Features registry entries — scan for any entry whose
            // DisplayName was the old app name and remove it.
            const string uninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            try
            {
                using var uninstallKey = Registry.CurrentUser.OpenSubKey(uninstallRoot, true);
                if (uninstallKey != null)
                {
                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var sub = uninstallKey.OpenSubKey(subKeyName);
                            var displayName = sub?.GetValue("DisplayName") as string;
                            if (displayName != null &&
                                (displayName.Equals("Windows Screen Logger", StringComparison.OrdinalIgnoreCase) ||
                                 displayName.Equals("WindowsScreenLogger", StringComparison.OrdinalIgnoreCase)))
                            {
                                sub?.Close();
                                uninstallKey.DeleteSubKey(subKeyName, false);
                                Debug.WriteLine($"Legacy cleanup: removed old Apps entry '{subKeyName}'");
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine($"Legacy cleanup: error checking subkey {subKeyName}: {ex.Message}"); }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Legacy cleanup: could not scan uninstall registry: {ex.Message}"); }
        }

        private static void ExtractEmbeddedBinary(string resourceName, string destinationPath)
        {
            using var stream = typeof(SelfInstaller).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Debug.WriteLine($"Embedded binary '{resourceName}' not found — skipping.");
                return;
            }
            using var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(file);
        }

        /// <summary>
        /// Shuts down any running instances of the application
        /// </summary>
        /// <param name="quiet">Whether to suppress user interaction</param>
        /// <returns>True if all instances were successfully shut down, false otherwise</returns>
        public static bool ShutdownRunningInstances(bool quiet = false)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Checking for running WindowsActivityLogger processes");
                
                // Get current process to avoid terminating ourselves if we're the uninstaller
                var currentProcess = Process.GetCurrentProcess();
                var currentProcessId = currentProcess.Id;
                
                // Find all WindowsActivityLogger processes
                var processes = Process.GetProcessesByName("WindowsActivityLogger")
                    .Where(p => p.Id != currentProcessId) // Don't terminate ourselves
                    .ToArray();
                
                if (processes.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No running WindowsActivityLogger instances found");
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine($"Found {processes.Length} running WindowsActivityLogger instance(s)");
                
                if (!quiet)
                {
                    var result = MessageBox.Show(
                        $"Windows Activity Logger is currently running ({processes.Length} instance{(processes.Length > 1 ? "s" : "")}).\n\n" +
                        "The application needs to be closed before uninstalling.\n\n" +
                        "Do you want to close it now and continue with the uninstall?",
                        "Close Running Application",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1);
                    
                    if (result != DialogResult.Yes)
                    {
                        System.Diagnostics.Debug.WriteLine("User declined to close running instances");
                        return false;
                    }
                }
                
                bool allClosed = true;
                foreach (var process in processes)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Attempting to close process {process.Id} gracefully");
                        
                        // Try to close the main window gracefully first
                        if (!process.CloseMainWindow())
                        {
                            System.Diagnostics.Debug.WriteLine($"Process {process.Id} has no main window to close");
                        }
                        
                        // Wait up to 10 seconds for graceful shutdown
                        bool exitedGracefully = process.WaitForExit(10000);
                        
                        if (!exitedGracefully)
                        {
                            System.Diagnostics.Debug.WriteLine($"Process {process.Id} did not exit gracefully, terminating forcefully");
                            process.Kill();
                            
                            // Wait up to 5 seconds for forced termination
                            if (!process.WaitForExit(5000))
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to terminate process {process.Id}");
                                allClosed = false;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Process {process.Id} terminated forcefully");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Process {process.Id} exited gracefully");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error terminating process {process.Id}: {ex.Message}");
                        allClosed = false;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                if (allClosed)
                {
                    // Additional wait to ensure all handles are released
                    System.Diagnostics.Debug.WriteLine("Waiting for handles to be released...");
                    Thread.Sleep(2000);
                    System.Diagnostics.Debug.WriteLine("All running instances successfully shut down");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to shut down all running instances");
                }
                
                return allClosed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex, "ShutdownRunningInstances");
                return false;
            }
        }

        public static void PerformUninstallation(bool quiet = false)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting PerformUninstallation - quiet: {quiet}");
                
                // Safety check - don't uninstall if not actually installed
                if (!IsInstalled())
                {
                    System.Diagnostics.Debug.WriteLine("Safety Check", false, "Application not installed");
                    if (!quiet)
                    {
                        MessageBox.Show("The application is not installed and cannot be uninstalled.", 
                            "Not Installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Application installation verified, proceeding with uninstallation");

                // Ensure all running instances are closed before proceeding
                System.Diagnostics.Debug.WriteLine("Checking for running instances to shut down");
                bool allInstancesClosed = ShutdownRunningInstances(quiet);
                
                if (!allInstancesClosed)
                {
                    System.Diagnostics.Debug.WriteLine("Not all running instances could be closed, proceeding with caution");
                }

                // Remove startup registration first
                System.Diagnostics.Debug.WriteLine("Removing startup registration");
                try
                {
                    StartupRegistry.SetStartupRegistration(false, InstalledExecutablePath);
                    System.Diagnostics.Debug.WriteLine("Startup Registry Removal", true, "Startup registration removed successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Startup Registry Removal", false, ex.Message);
                }

                // Unregister from Windows Apps
                System.Diagnostics.Debug.WriteLine("Unregistering from Windows Apps & Features");
                try
                {
                    WindowsAppsRegistry.UnregisterApplication();
                    System.Diagnostics.Debug.WriteLine("Windows Apps Registry Removal", true, "Successfully removed from Windows Apps & Features");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Windows Apps Registry Removal", false, ex.Message);
                }

                // Clean up configuration files and logs
                System.Diagnostics.Debug.WriteLine("Cleaning up configuration and log files");
                try
                {
                    CleanupConfigurationFiles();
                    System.Diagnostics.Debug.WriteLine("Configuration Cleanup", true, "Configuration and log files cleaned up");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Configuration Cleanup", false, ex.Message);
                }

                // Check if we can delete the parent directory immediately
                bool immediateDeleteSuccess = false;
                bool runningFromInstallLocation = IsRunningFromInstallLocation();
                System.Diagnostics.Debug.WriteLine($"Running from install location: {runningFromInstallLocation}");
                
                if (!runningFromInstallLocation && Directory.Exists(InstallPath))
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Attempting immediate deletion of: {InstallPath}");
                        Directory.Delete(InstallPath, true);
                        immediateDeleteSuccess = true;
                        System.Diagnostics.Debug.WriteLine("Immediate Directory Deletion", true, $"Successfully removed: {InstallPath}");
                        Debug.WriteLine($"Successfully removed installation directory immediately: {InstallPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Immediate Directory Deletion", false, ex.Message);
                        Debug.WriteLine($"Immediate deletion failed (expected if running from install location): {ex.Message}");
                    }
                }

                if (!immediateDeleteSuccess && Directory.Exists(InstallPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Scheduling delayed deletion of installation directory: {InstallPath}");
                    Debug.WriteLine($"Scheduling delayed deletion of installation directory: {InstallPath}");
                    
                    // Use delayed deletion with both PowerShell and batch fallback
                    try
                    {
                        UninstallScriptManager.ExecutePowerShellUninstaller(InstallPath);
                        System.Diagnostics.Debug.WriteLine("Delayed Deletion (PowerShell)", true, "PowerShell uninstaller script created");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Delayed Deletion (PowerShell)", false, ex.Message);
                        Debug.WriteLine($"PowerShell uninstaller creation failed, using batch fallback: {ex.Message}");
                        // Fallback to batch file if PowerShell fails
                        try
                        {
                            UninstallScriptManager.ExecuteBatchUninstaller(InstallPath);
                            System.Diagnostics.Debug.WriteLine("Delayed Deletion (Batch)", true, "Batch uninstaller script created as fallback");
                        }
                        catch (Exception batchEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Delayed Deletion (Batch)", false, batchEx.Message);
                        }
                    }
                }

                // Show completion message before shutting down
                if (!quiet)
                {
                    string message = immediateDeleteSuccess 
                        ? $"{AppName} has been successfully uninstalled.\n\nThe installation folder has been completely removed."
                        : $"{AppName} has been successfully uninstalled.\n\n" +
                          "The installation folder and all associated files will be completely removed after this dialog is closed.";
                    
                    System.Diagnostics.Debug.WriteLine($"Showing completion message to user: {(immediateDeleteSuccess ? "immediate" : "delayed")} deletion");
                    MessageBox.Show(message, "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Initiate graceful shutdown
                System.Diagnostics.Debug.WriteLine("Initiating graceful application shutdown");
                InitiateGracefulShutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Complete Process", false, ex.Message);
                System.Diagnostics.Debug.WriteLine(ex, "PerformUninstallation");
                
                if (!quiet)
                {
                    MessageBox.Show($"Uninstallation failed: {ex.Message}", 
                        "Uninstall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Cleans up configuration files and logs
        /// </summary>
        private static void CleanupConfigurationFiles()
        {
            try
            {
                // Clean up application data folder
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsActivityLogger");
                if (Directory.Exists(appDataPath))
                {
                    try
                    {
                        Directory.Delete(appDataPath, true);
                        System.Diagnostics.Debug.WriteLine($"Deleted application data folder: {appDataPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete application data folder {appDataPath}: {ex.Message}");
                    }
                }

                // Clean up any temporary files
                var tempPath = Path.GetTempPath();
                var tempFiles = Directory.GetFiles(tempPath, "*screenlogger*", SearchOption.TopDirectoryOnly);
                foreach (var tempFile in tempFiles)
                {
                    try
                    {
                        File.Delete(tempFile);
                        System.Diagnostics.Debug.WriteLine($"Deleted temporary file: {tempFile}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete temporary file {tempFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during configuration cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Initiates a graceful shutdown of the application
        /// </summary>
        private static void InitiateGracefulShutdown()
        {
            try
            {
                // Log final shutdown message
                System.Diagnostics.Debug.WriteLine("=== Application uninstalled and shutting down ===");
                
                // Ensure all pending operations complete
                Application.DoEvents();
                
                // Give a moment for any final logging to complete
                Thread.Sleep(100);
                
                // Use a background task to handle the shutdown to avoid blocking
                Task.Run(() =>
                {
                    try
                    {
                        // Small delay to ensure dialog closes properly
                        Thread.Sleep(500);
                        
                        // Try graceful exit first
                        System.Diagnostics.Debug.WriteLine("Attempting graceful application exit");
                        Application.Exit();
                        
                        // Give it a moment to exit gracefully
                        Thread.Sleep(1000);
                        
                        // If we're still here, force exit
                        System.Diagnostics.Debug.WriteLine("Forcing application termination");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
                        Environment.Exit(1);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initiating graceful shutdown: {ex.Message}");
                // Force immediate exit if graceful shutdown fails
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Gets detailed information about the installation status
        /// </summary>
        public static string GetInstallationStatus()
        {
            if (!Directory.Exists(InstallPath))
            {
                return "Not installed - installation directory does not exist";
            }

            if (!File.Exists(InstalledExecutablePath))
            {
                return "Installation directory exists but executable is missing";
            }

            try
            {
                var files = Directory.GetFiles(InstallPath, "*.*", SearchOption.AllDirectories);
                var dirs = Directory.GetDirectories(InstallPath, "*", SearchOption.AllDirectories);
                return $"Installed - {files.Length} files in {dirs.Length + 1} directories";
            }
            catch
            {
                return "Installed - cannot enumerate contents";
            }
        }
    }
}
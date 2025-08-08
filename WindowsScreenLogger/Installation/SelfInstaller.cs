using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace WindowsScreenLogger.Installation
{
    public static class SelfInstaller
    {
        private const string AppName = "Windows Screen Logger";

        public static string InstallPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        public static string InstalledExecutablePath => Path.Combine(InstallPath, "WindowsScreenLogger.exe");

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
                "• Copy the application to your user folder\n" +
                "• Add it to Windows Apps & Features\n" +
                "• Enable proper startup with Windows\n" +
                "• Allow easy uninstallation\n" +
                "• No administrator privileges required\n\n" +
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

                // Register in Windows Apps & Features (user registry only)
                WindowsAppsRegistry.RegisterApplication(InstallPath, InstalledExecutablePath);

                // Set startup registry entry to installed location
                StartupRegistry.SetStartupRegistration(true, InstalledExecutablePath);

                // Use delayed start to avoid mutex conflict - no blocking dialogs
                StartInstalledVersionWithDelay();
                
                // Simple, direct exit approach
                Application.Exit();
                
                // Give a brief moment for graceful exit, then force if needed
                Thread.Sleep(300);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed: {ex.Message}", 
                    "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Starts the installed version with a delay to ensure the current process has time to exit
        /// </summary>
        private static void StartInstalledVersionWithDelay()
        {
            try
            {
                // Create a batch script that waits and then starts the installed version
                string tempBatchFile = Path.Combine(Path.GetTempPath(), "start_installed_screenlogger.bat");
                
                string batchContent = $@"@echo off
rem Wait for the current process to fully exit
timeout /t 2 /nobreak >nul

rem Verify the installed executable exists
if not exist ""{InstalledExecutablePath}"" (
    exit /b 1
)

rem Start the installed version with a special flag
start """" ""{InstalledExecutablePath}"" --post-install

rem Wait a moment to ensure the process starts
timeout /t 1 /nobreak >nul

rem Delete this batch file
del ""%~f0"" >nul 2>&1
";

                File.WriteAllText(tempBatchFile, batchContent);
                
                // Start the batch file and let it handle the delayed execution
                var startInfo = new ProcessStartInfo
                {
                    FileName = tempBatchFile,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                // Fallback: try direct start with special flag after a shorter delay
                Debug.WriteLine($"Failed to create delayed start script: {ex.Message}");
                try
                {
                    // Use a background task for the fallback to avoid blocking
                    Task.Run(async () =>
                    {
                        await Task.Delay(1500); // Shorter delay for fallback
                        try
                        {
                            Process.Start(new ProcessStartInfo(InstalledExecutablePath, "--post-install")
                            {
                                UseShellExecute = true
                            });
                        }
                        catch (Exception fallbackEx)
                        {
                            Debug.WriteLine($"Fallback start also failed: {fallbackEx.Message}");
                        }
                    });
                }
                catch (Exception taskEx)
                {
                    Debug.WriteLine($"Failed to create fallback task: {taskEx.Message}");
                }
            }
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
                AppLogger.LogInformation("Checking for running WindowsScreenLogger processes");
                
                // Get current process to avoid terminating ourselves if we're the uninstaller
                var currentProcess = Process.GetCurrentProcess();
                var currentProcessId = currentProcess.Id;
                
                // Find all WindowsScreenLogger processes
                var processes = Process.GetProcessesByName("WindowsScreenLogger")
                    .Where(p => p.Id != currentProcessId) // Don't terminate ourselves
                    .ToArray();
                
                if (processes.Length == 0)
                {
                    AppLogger.LogInformation("No running WindowsScreenLogger instances found");
                    return true;
                }
                
                AppLogger.LogInformation($"Found {processes.Length} running WindowsScreenLogger instance(s)");
                
                if (!quiet)
                {
                    var result = MessageBox.Show(
                        $"Windows Screen Logger is currently running ({processes.Length} instance{(processes.Length > 1 ? "s" : "")}).\n\n" +
                        "The application needs to be closed before uninstalling.\n\n" +
                        "Do you want to close it now and continue with the uninstall?",
                        "Close Running Application",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1);
                    
                    if (result != DialogResult.Yes)
                    {
                        AppLogger.LogInformation("User declined to close running instances");
                        return false;
                    }
                }
                
                bool allClosed = true;
                foreach (var process in processes)
                {
                    try
                    {
                        AppLogger.LogInformation($"Attempting to close process {process.Id} gracefully");
                        
                        // Try to close the main window gracefully first
                        if (!process.CloseMainWindow())
                        {
                            AppLogger.LogWarning($"Process {process.Id} has no main window to close");
                        }
                        
                        // Wait up to 10 seconds for graceful shutdown
                        bool exitedGracefully = process.WaitForExit(10000);
                        
                        if (!exitedGracefully)
                        {
                            AppLogger.LogWarning($"Process {process.Id} did not exit gracefully, terminating forcefully");
                            process.Kill();
                            
                            // Wait up to 5 seconds for forced termination
                            if (!process.WaitForExit(5000))
                            {
                                AppLogger.LogError($"Failed to terminate process {process.Id}");
                                allClosed = false;
                            }
                            else
                            {
                                AppLogger.LogInformation($"Process {process.Id} terminated forcefully");
                            }
                        }
                        else
                        {
                            AppLogger.LogInformation($"Process {process.Id} exited gracefully");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError($"Error terminating process {process.Id}: {ex.Message}");
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
                    AppLogger.LogDebug("Waiting for handles to be released...");
                    Thread.Sleep(2000);
                    AppLogger.LogInformation("All running instances successfully shut down");
                }
                else
                {
                    AppLogger.LogError("Failed to shut down all running instances");
                }
                
                return allClosed;
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "ShutdownRunningInstances");
                return false;
            }
        }

        public static void PerformUninstallation(bool quiet = false)
        {
            try
            {
                AppLogger.LogInformation($"Starting PerformUninstallation - quiet: {quiet}");
                
                // Safety check - don't uninstall if not actually installed
                if (!IsInstalled())
                {
                    AppLogger.LogUninstallOperation("Safety Check", false, "Application not installed");
                    if (!quiet)
                    {
                        MessageBox.Show("The application is not installed and cannot be uninstalled.", 
                            "Not Installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                AppLogger.LogInformation("Application installation verified, proceeding with uninstallation");

                // Ensure all running instances are closed before proceeding
                AppLogger.LogDebug("Checking for running instances to shut down");
                bool allInstancesClosed = ShutdownRunningInstances(quiet);
                
                if (!allInstancesClosed)
                {
                    AppLogger.LogWarning("Not all running instances could be closed, proceeding with caution");
                }

                // Remove startup registration first
                AppLogger.LogDebug("Removing startup registration");
                try
                {
                    StartupRegistry.SetStartupRegistration(false, InstalledExecutablePath);
                    AppLogger.LogUninstallOperation("Startup Registry Removal", true, "Startup registration removed successfully");
                }
                catch (Exception ex)
                {
                    AppLogger.LogUninstallOperation("Startup Registry Removal", false, ex.Message);
                }

                // Unregister from Windows Apps
                AppLogger.LogDebug("Unregistering from Windows Apps & Features");
                try
                {
                    WindowsAppsRegistry.UnregisterApplication();
                    AppLogger.LogUninstallOperation("Windows Apps Registry Removal", true, "Successfully removed from Windows Apps & Features");
                }
                catch (Exception ex)
                {
                    AppLogger.LogUninstallOperation("Windows Apps Registry Removal", false, ex.Message);
                }

                // Clean up configuration files and logs
                AppLogger.LogDebug("Cleaning up configuration and log files");
                try
                {
                    CleanupConfigurationFiles();
                    AppLogger.LogUninstallOperation("Configuration Cleanup", true, "Configuration and log files cleaned up");
                }
                catch (Exception ex)
                {
                    AppLogger.LogUninstallOperation("Configuration Cleanup", false, ex.Message);
                }

                // Check if we can delete the parent directory immediately
                bool immediateDeleteSuccess = false;
                bool runningFromInstallLocation = IsRunningFromInstallLocation();
                AppLogger.LogDebug($"Running from install location: {runningFromInstallLocation}");
                
                if (!runningFromInstallLocation && Directory.Exists(InstallPath))
                {
                    try
                    {
                        AppLogger.LogDebug($"Attempting immediate deletion of: {InstallPath}");
                        Directory.Delete(InstallPath, true);
                        immediateDeleteSuccess = true;
                        AppLogger.LogUninstallOperation("Immediate Directory Deletion", true, $"Successfully removed: {InstallPath}");
                        Debug.WriteLine($"Successfully removed installation directory immediately: {InstallPath}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogUninstallOperation("Immediate Directory Deletion", false, ex.Message);
                        Debug.WriteLine($"Immediate deletion failed (expected if running from install location): {ex.Message}");
                    }
                }

                if (!immediateDeleteSuccess && Directory.Exists(InstallPath))
                {
                    AppLogger.LogDebug($"Scheduling delayed deletion of installation directory: {InstallPath}");
                    Debug.WriteLine($"Scheduling delayed deletion of installation directory: {InstallPath}");
                    
                    // Use delayed deletion with both PowerShell and batch fallback
                    try
                    {
                        UninstallScriptManager.ExecutePowerShellUninstaller(InstallPath);
                        AppLogger.LogUninstallOperation("Delayed Deletion (PowerShell)", true, "PowerShell uninstaller script created");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogUninstallOperation("Delayed Deletion (PowerShell)", false, ex.Message);
                        Debug.WriteLine($"PowerShell uninstaller creation failed, using batch fallback: {ex.Message}");
                        // Fallback to batch file if PowerShell fails
                        try
                        {
                            UninstallScriptManager.ExecuteBatchUninstaller(InstallPath);
                            AppLogger.LogUninstallOperation("Delayed Deletion (Batch)", true, "Batch uninstaller script created as fallback");
                        }
                        catch (Exception batchEx)
                        {
                            AppLogger.LogUninstallOperation("Delayed Deletion (Batch)", false, batchEx.Message);
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
                    
                    AppLogger.LogInformation($"Showing completion message to user: {(immediateDeleteSuccess ? "immediate" : "delayed")} deletion");
                    MessageBox.Show(message, "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Initiate graceful shutdown
                AppLogger.LogInformation("Initiating graceful application shutdown");
                InitiateGracefulShutdown();
            }
            catch (Exception ex)
            {
                AppLogger.LogUninstallOperation("Complete Process", false, ex.Message);
                AppLogger.LogException(ex, "PerformUninstallation");
                
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
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsScreenLogger");
                if (Directory.Exists(appDataPath))
                {
                    try
                    {
                        Directory.Delete(appDataPath, true);
                        AppLogger.LogDebug($"Deleted application data folder: {appDataPath}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogWarning($"Failed to delete application data folder {appDataPath}: {ex.Message}");
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
                        AppLogger.LogDebug($"Deleted temporary file: {tempFile}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogDebug($"Failed to delete temporary file {tempFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"Error during configuration cleanup: {ex.Message}");
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
                AppLogger.LogInformation("=== Application uninstalled and shutting down ===");
                
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
                        AppLogger.LogDebug("Attempting graceful application exit");
                        Application.Exit();
                        
                        // Give it a moment to exit gracefully
                        Thread.Sleep(1000);
                        
                        // If we're still here, force exit
                        AppLogger.LogDebug("Forcing application termination");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError($"Error during shutdown: {ex.Message}");
                        Environment.Exit(1);
                    }
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error initiating graceful shutdown: {ex.Message}");
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
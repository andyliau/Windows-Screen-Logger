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

        public static void PerformUninstallation(bool quiet = false)
        {
            try
            {
                // Safety check - don't uninstall if not actually installed
                if (!IsInstalled())
                {
                    if (!quiet)
                    {
                        MessageBox.Show("The application is not installed and cannot be uninstalled.", 
                            "Not Installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                // Remove startup registration first
                StartupRegistry.SetStartupRegistration(false, InstalledExecutablePath);

                // Unregister from Windows Apps
                WindowsAppsRegistry.UnregisterApplication();

                // Check if we can delete the parent directory immediately
                bool immediateDeleteSuccess = false;
                if (!IsRunningFromInstallLocation() && Directory.Exists(InstallPath))
                {
                    try
                    {
                        Directory.Delete(InstallPath, true);
                        immediateDeleteSuccess = true;
                        Debug.WriteLine($"Successfully removed installation directory immediately: {InstallPath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Immediate deletion failed (expected if running from install location): {ex.Message}");
                    }
                }

                if (!immediateDeleteSuccess && Directory.Exists(InstallPath))
                {
                    Debug.WriteLine($"Scheduling delayed deletion of installation directory: {InstallPath}");
                    
                    // Use delayed deletion with both PowerShell and batch fallback
                    try
                    {
                        UninstallScriptManager.ExecutePowerShellUninstaller(InstallPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"PowerShell uninstaller creation failed, using batch fallback: {ex.Message}");
                        // Fallback to batch file if PowerShell fails
                        UninstallScriptManager.ExecuteBatchUninstaller(InstallPath);
                    }
                }

                if (!quiet)
                {
                    string message = immediateDeleteSuccess 
                        ? $"{AppName} has been successfully uninstalled.\n\nThe installation folder has been completely removed."
                        : $"{AppName} has been successfully uninstalled.\n\n" +
                          "The installation folder will be completely removed after this dialog is closed.";
                    
                    MessageBox.Show(message, "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                Application.Exit();
            }
            catch (Exception ex)
            {
                if (!quiet)
                {
                    MessageBox.Show($"Uninstallation failed: {ex.Message}", 
                        "Uninstall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Verifies that the installation directory has been completely removed
        /// </summary>
        public static bool IsCompletelyUninstalled()
        {
            // Check if installation directory exists
            if (Directory.Exists(InstallPath))
            {
                // If directory exists, check if it's empty
                try
                {
                    return !Directory.EnumerateFileSystemEntries(InstallPath).Any();
                }
                catch
                {
                    // If we can't enumerate, assume it still exists
                    return false;
                }
            }
            
            // Directory doesn't exist, so it's completely removed
            return true;
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
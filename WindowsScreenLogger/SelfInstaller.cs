using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace WindowsScreenLogger
{
    public static class SelfInstaller
    {
        private const string AppName = "Windows Screen Logger";
        private const string AppVersion = "1.0.0";
        private const string AppPublisher = "WindowsScreenLogger";
        private const string AppGuid = "{B3E7C6A8-9F2D-4E5A-B1C3-8D7F6E9A2B4C}";

        public static string InstallPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);
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
                "• Copy the application to Program Files\n" +
                "• Add it to Windows Apps & Features\n" +
                "• Enable proper startup with Windows\n" +
                "• Allow easy uninstallation\n\n" +
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
                if (!IsElevated())
                {
                    // Restart with elevated permissions
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = "/install",
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    try
                    {
                        Process.Start(startInfo);
                        Application.Exit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to request administrator privileges: {ex.Message}", 
                            "Installation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;
                }

                // Create installation directory
                Directory.CreateDirectory(InstallPath);

                // Copy executable
                File.Copy(Application.ExecutablePath, InstalledExecutablePath, true);

                // Register in Windows Apps & Features
                RegisterInWindowsApps();

                // Set startup registry entry to installed location
                SetStartupRegistration(true);

                MessageBox.Show($"{AppName} has been successfully installed!\n\n" +
                    $"Installation location: {InstallPath}\n\n" +
                    "The application will now restart from the installed location.",
                    "Installation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Start the installed version
                Process.Start(InstalledExecutablePath);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed: {ex.Message}", 
                    "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void RegisterInWindowsApps()
        {
            try
            {
                string uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppGuid}";

                using var key = Registry.LocalMachine.CreateSubKey(uninstallKey);
                if (key != null)
                {
                    key.SetValue("DisplayName", AppName);
                    key.SetValue("DisplayVersion", AppVersion);
                    key.SetValue("Publisher", AppPublisher);
                    key.SetValue("InstallLocation", InstallPath);
                    key.SetValue("UninstallString", $"\"{InstalledExecutablePath}\" /uninstall");
                    key.SetValue("QuietUninstallString", $"\"{InstalledExecutablePath}\" /uninstall /quiet");
                    key.SetValue("DisplayIcon", InstalledExecutablePath);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("EstimatedSize", GetInstallationSize(), RegistryValueKind.DWord);
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    key.SetValue("HelpLink", "");
                    key.SetValue("URLInfoAbout", "");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to register application in Windows Apps: {ex.Message}");
            }
        }

        public static void UnregisterFromWindowsApps()
        {
            try
            {
                string uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppGuid}";
                Registry.LocalMachine.DeleteSubKey(uninstallKey, false);
            }
            catch (Exception ex)
            {
                // Log but don't throw - uninstall should continue
                Debug.WriteLine($"Failed to unregister from Windows Apps: {ex.Message}");
            }
        }

        public static void SetStartupRegistration(bool enable)
        {
            try
            {
                const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
                
                if (enable && IsInstalled())
                {
                    key?.SetValue(AppName, InstalledExecutablePath);
                }
                else
                {
                    key?.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set startup registration: {ex.Message}");
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

                if (!IsElevated())
                {
                    // Restart with elevated permissions
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = quiet ? "/uninstall /quiet" : "/uninstall",
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    try
                    {
                        Process.Start(startInfo);
                        Application.Exit();
                    }
                    catch (Exception ex)
                    {
                        if (!quiet)
                        {
                            MessageBox.Show($"Failed to request administrator privileges: {ex.Message}", 
                                "Uninstall Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    return;
                }

                // Remove startup registration first
                SetStartupRegistration(false);

                // Unregister from Windows Apps
                UnregisterFromWindowsApps();

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
                    
                    // Use delayed deletion with both batch and PowerShell fallback
                    try
                    {
                        CreatePowerShellUninstaller();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"PowerShell uninstaller creation failed, using batch fallback: {ex.Message}");
                        // Fallback to batch file if PowerShell fails
                        string batchFile = CreateSelfDeleteBatch();
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = batchFile,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(startInfo);
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

        private static string CreateSelfDeleteBatch()
        {
            string tempBatchFile = Path.Combine(Path.GetTempPath(), "uninstall_screenlogger.bat");
            
            string batchContent = $@"@echo off
rem Wait for the application to fully exit
timeout /t 3 /nobreak >nul

rem Delete the installation directory (parent folder) with all contents
if exist ""{InstallPath}"" (
    echo Removing installation directory: {InstallPath}
    rmdir /s /q ""{InstallPath}""
    if exist ""{InstallPath}"" (
        echo Failed to remove directory with rmdir, trying alternative method...
        rd /s /q ""{InstallPath}""
    )
    if exist ""{InstallPath}"" (
        echo Warning: Installation directory could not be completely removed
    ) else (
        echo Installation directory successfully removed
    )
)

rem Delete this batch file
del ""%~f0""
";

            File.WriteAllText(tempBatchFile, batchContent);
            return tempBatchFile;
        }

        /// <summary>
        /// Alternative uninstall method using PowerShell for more robust deletion
        /// </summary>
        private static void CreatePowerShellUninstaller()
        {
            string tempPsFile = Path.Combine(Path.GetTempPath(), "uninstall_screenlogger.ps1");
            
            string psContent = $@"
# Wait for the application to fully exit
Start-Sleep -Seconds 3

# Delete the installation directory (parent folder) with all contents
if (Test-Path ""{InstallPath}"") {{
    Write-Host ""Removing installation directory: {InstallPath}""
    try {{
        # Primary method: PowerShell Remove-Item with force and recurse
        Remove-Item ""{InstallPath}"" -Recurse -Force -ErrorAction Stop
        Write-Host ""Installation directory successfully removed using PowerShell""
    }}
    catch {{
        Write-Host ""Failed to remove installation directory with PowerShell: $_""
        Write-Host ""Trying alternative method with cmd...""
        try {{
            # Fallback method: cmd rmdir
            Start-Process cmd -ArgumentList ""/c rmdir /s /q `""{InstallPath}`"""" -WindowStyle Hidden -Wait -ErrorAction Stop
            if (-not (Test-Path ""{InstallPath}"")) {{
                Write-Host ""Installation directory successfully removed using cmd""
            }} else {{
                Write-Host ""Warning: Installation directory could not be completely removed""
            }}
        }}
        catch {{
            Write-Host ""Failed to remove installation directory with cmd: $_""
            # Final attempt: try to remove files individually
            try {{
                Get-ChildItem ""{InstallPath}"" -Recurse -Force | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
                Remove-Item ""{InstallPath}"" -Force -ErrorAction SilentlyContinue
                if (-not (Test-Path ""{InstallPath}"")) {{
                    Write-Host ""Installation directory successfully removed using individual file deletion""
                }} else {{
                    Write-Host ""Warning: Some files may remain in the installation directory""
                }}
            }}
            catch {{
                Write-Host ""Final cleanup attempt failed: $_""
            }}
        }}
    }}
}} else {{
    Write-Host ""Installation directory not found: {InstallPath}""
}}

# Remove this script
Remove-Item $PSCommandPath -Force -ErrorAction SilentlyContinue
";

            File.WriteAllText(tempPsFile, psContent);
            
            // Start PowerShell with execution policy bypass
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempPsFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
        }

        private static int GetInstallationSize()
        {
            try
            {
                var fileInfo = new FileInfo(Application.ExecutablePath);
                return (int)(fileInfo.Length / 1024); // Size in KB
            }
            catch
            {
                return 80000; // Default estimate in KB (~80MB)
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
using System.Diagnostics;
using System.Reflection;

namespace WindowsScreenLogger.Installation
{
    /// <summary>
    /// Manages uninstallation scripts for delayed deletion
    /// </summary>
    public static class UninstallScriptManager
    {
        /// <summary>
        /// Creates and executes a PowerShell script for delayed deletion
        /// </summary>
        public static void ExecutePowerShellUninstaller(string installPath)
        {
            string tempPsFile = Path.Combine(Path.GetTempPath(), "uninstall_screenlogger.ps1");
            
            // Extract PowerShell script from embedded resources
            string psContent = GetEmbeddedScript("UninstallScript.ps1");
            File.WriteAllText(tempPsFile, psContent);
            
            // Start PowerShell with execution policy bypass
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempPsFile}\" -InstallPath \"{installPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
        }

        /// <summary>
        /// Creates and executes a batch script for delayed deletion
        /// </summary>
        public static void ExecuteBatchUninstaller(string installPath)
        {
            string tempBatchFile = Path.Combine(Path.GetTempPath(), "uninstall_screenlogger.bat");
            
            // Extract batch script from embedded resources
            string batchContent = GetEmbeddedScript("UninstallScript.bat");
            
            // Replace placeholder with actual install path
            batchContent = batchContent.Replace("%~1", $"\"{installPath}\"");
            File.WriteAllText(tempBatchFile, batchContent);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = tempBatchFile,
                Arguments = $"\"{installPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);
        }

        private static string GetEmbeddedScript(string scriptName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = $"WindowsScreenLogger.Installation.{scriptName}";
            
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load embedded script {scriptName}: {ex.Message}");
            }

            // Fallback: return basic script content if embedded resource fails
            return scriptName.EndsWith(".ps1") ? GetFallbackPowerShellScript() : GetFallbackBatchScript();
        }

        private static string GetFallbackPowerShellScript()
        {
            return @"
param([string]$InstallPath)
Start-Sleep -Seconds 3
if (Test-Path $InstallPath) {
    try { Remove-Item $InstallPath -Recurse -Force }
    catch { Start-Process cmd -ArgumentList ""/c rmdir /s /q `""$InstallPath`"""" -WindowStyle Hidden -Wait }
}
Remove-Item $PSCommandPath -Force -ErrorAction SilentlyContinue
";
        }

        private static string GetFallbackBatchScript()
        {
            return @"@echo off
timeout /t 3 /nobreak >nul
if exist ""%~1"" rmdir /s /q ""%~1""
del ""%~f0""
";
        }
    }
}
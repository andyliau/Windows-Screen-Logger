using Microsoft.Win32;
using System.Diagnostics;

namespace WindowsScreenLogger.Installation
{
    /// <summary>
    /// Handles Windows Apps & Features registry operations
    /// </summary>
    public static class WindowsAppsRegistry
    {
        private const string AppName = "Windows Screen Logger";
        private const string AppVersion = "1.0.0";
        private const string AppPublisher = "WindowsScreenLogger";
        private const string AppGuid = "{B3E7C6A8-9F2D-4E5A-B1C3-8D7F6E9A2B4C}";

        public static void RegisterApplication(string installPath, string executablePath)
        {
            try
            {
                string uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppGuid}";
                using var userKey = Registry.CurrentUser.CreateSubKey(uninstallKey);
                if (userKey != null)
                {
                    SetRegistryValues(userKey, installPath, executablePath);
                    AppLogger.LogRegistryOperation("RegisterApplication", uninstallKey, true, "Successfully registered in Windows Apps & Features");
                    Debug.WriteLine("Successfully registered in current user Windows Apps");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogRegistryOperation("RegisterApplication", AppGuid, false, ex.Message);
                throw new Exception($"Failed to register application in Windows Apps: {ex.Message}");
            }
        }

        public static void UnregisterApplication()
        {
            try
            {
                string uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppGuid}";
                Registry.CurrentUser.DeleteSubKey(uninstallKey, false);
                AppLogger.LogRegistryOperation("UnregisterApplication", uninstallKey, true, "Successfully removed from Windows Apps & Features");
                Debug.WriteLine("Successfully unregistered from current user Windows Apps");
            }
            catch (Exception ex)
            {
                AppLogger.LogRegistryOperation("UnregisterApplication", AppGuid, false, ex.Message);
                Debug.WriteLine($"Failed to unregister from current user registry: {ex.Message}");
            }
        }

        private static void SetRegistryValues(RegistryKey key, string installPath, string executablePath)
        {
            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", AppVersion);
            key.SetValue("Publisher", AppPublisher);
            key.SetValue("InstallLocation", installPath);
            
            // Use the correct command format for System.CommandLine
            key.SetValue("UninstallString", $"\"{executablePath}\" uninstall");
            key.SetValue("QuietUninstallString", $"\"{executablePath}\" uninstall --quiet");
            
            key.SetValue("DisplayIcon", executablePath);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", GetInstallationSize(executablePath), RegistryValueKind.DWord);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("HelpLink", "");
            key.SetValue("URLInfoAbout", "");

            AppLogger.LogDebug($"Registry values set - UninstallString: \"{executablePath}\" uninstall");
            AppLogger.LogDebug($"Registry values set - QuietUninstallString: \"{executablePath}\" uninstall --quiet");
        }

        private static int GetInstallationSize(string executablePath)
        {
            try
            {
                var fileInfo = new FileInfo(executablePath);
                return (int)(fileInfo.Length / 1024); // Size in KB
            }
            catch
            {
                return 80000; // Default estimate in KB (~80MB)
            }
        }

        /// <summary>
        /// Gets the current uninstall strings from the registry for debugging
        /// </summary>
        public static (string? uninstallString, string? quietUninstallString) GetRegisteredUninstallStrings()
        {
            try
            {
                string uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppGuid}";
                using var userKey = Registry.CurrentUser.OpenSubKey(uninstallKey);
                if (userKey != null)
                {
                    var uninstallString = userKey.GetValue("UninstallString")?.ToString();
                    var quietUninstallString = userKey.GetValue("QuietUninstallString")?.ToString();
                    
                    AppLogger.LogDebug($"Current UninstallString: {uninstallString ?? "NULL"}");
                    AppLogger.LogDebug($"Current QuietUninstallString: {quietUninstallString ?? "NULL"}");
                    
                    return (uninstallString, quietUninstallString);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogDebug($"Failed to read uninstall strings from registry: {ex.Message}");
            }
            
            return (null, null);
        }

        /// <summary>
        /// Validates that the registry entries are correctly formatted
        /// </summary>
        public static bool ValidateRegistryEntries()
        {
            var (uninstallString, quietUninstallString) = GetRegisteredUninstallStrings();
            
            bool isValid = true;
            
            if (string.IsNullOrEmpty(uninstallString))
            {
                AppLogger.LogWarning("UninstallString is missing or empty");
                isValid = false;
            }
            else if (!uninstallString.Contains("uninstall") || uninstallString.Contains("/uninstall"))
            {
                AppLogger.LogWarning($"UninstallString format incorrect: {uninstallString}");
                isValid = false;
            }
            
            if (string.IsNullOrEmpty(quietUninstallString))
            {
                AppLogger.LogWarning("QuietUninstallString is missing or empty");
                isValid = false;
            }
            else if (!quietUninstallString.Contains("--quiet") || quietUninstallString.Contains("/quiet"))
            {
                AppLogger.LogWarning($"QuietUninstallString format incorrect: {quietUninstallString}");
                isValid = false;
            }
            
            AppLogger.LogInformation($"Registry entries validation result: {(isValid ? "VALID" : "INVALID")}");
            return isValid;
        }
    }
}
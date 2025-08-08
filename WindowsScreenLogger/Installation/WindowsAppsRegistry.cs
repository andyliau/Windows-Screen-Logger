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
                    Debug.WriteLine("Successfully registered in current user Windows Apps");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to register application in Windows Apps: {ex.Message}");
            }
        }

        public static void UnregisterApplication()
        {
            try
            {
                string uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppGuid}";
                Registry.CurrentUser.DeleteSubKey(uninstallKey, false);
                Debug.WriteLine("Successfully unregistered from current user Windows Apps");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to unregister from current user registry: {ex.Message}");
            }
        }

        private static void SetRegistryValues(RegistryKey key, string installPath, string executablePath)
        {
            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", AppVersion);
            key.SetValue("Publisher", AppPublisher);
            key.SetValue("InstallLocation", installPath);
            key.SetValue("UninstallString", $"\"{executablePath}\" /uninstall");
            key.SetValue("QuietUninstallString", $"\"{executablePath}\" /uninstall /quiet");
            key.SetValue("DisplayIcon", executablePath);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", GetInstallationSize(executablePath), RegistryValueKind.DWord);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("HelpLink", "");
            key.SetValue("URLInfoAbout", "");
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
    }
}
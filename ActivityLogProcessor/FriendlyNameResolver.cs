using System.Diagnostics;
using Microsoft.Win32;

namespace ActivityLogProcessor;

/// <summary>
/// Resolves a process name to a human-readable application name.
///
/// Priority chain (first match wins):
///   1. User overrides  — %APPDATA%\WindowsScreenLogger\friendly-names.json
///   2. Registry lookup — HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{name}.exe → ProductName
///   3. Hardcoded       — small fallback table for common apps
/// </summary>
public static class FriendlyNameResolver
{
    private static readonly IReadOnlyDictionary<string, string> Hardcoded =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"]         = "Visual Studio Code",
            ["devenv"]       = "Visual Studio",
            ["chrome"]       = "Google Chrome",
            ["msedge"]       = "Microsoft Edge",
            ["firefox"]      = "Mozilla Firefox",
            ["teams"]        = "Microsoft Teams",
            ["slack"]        = "Slack",
            ["outlook"]      = "Microsoft Outlook",
            ["explorer"]     = "Windows Explorer",
            ["notepad"]      = "Notepad",
            ["notepad++"]    = "Notepad++",
            ["rider"]        = "JetBrains Rider",
            ["idea64"]       = "IntelliJ IDEA",
            ["pycharm64"]    = "PyCharm",
            ["webstorm64"]   = "WebStorm",
            ["datagrip64"]   = "DataGrip",
            ["discord"]      = "Discord",
            ["spotify"]      = "Spotify",
            ["powerpnt"]     = "PowerPoint",
            ["winword"]      = "Word",
            ["excel"]        = "Excel",
            ["onenote"]      = "OneNote",
            ["mspaint"]      = "Paint",
            ["wt"]           = "Windows Terminal",
            ["WindowsTerminal"] = "Windows Terminal",
            ["pwsh"]         = "PowerShell",
            ["powershell"]   = "PowerShell",
            ["cmd"]          = "Command Prompt",
            ["taskmgr"]      = "Task Manager",
            ["mmc"]          = "Management Console",
            ["regedit"]      = "Registry Editor",
        };

    private static readonly Lazy<IReadOnlyDictionary<string, string>> UserOverrides =
        new(LoadUserOverrides);

    private static readonly Dictionary<string, string?> ResolvedCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string? Resolve(string processName)
    {
        if (ResolvedCache.TryGetValue(processName, out var cached))
            return cached;

        if (UserOverrides.Value.TryGetValue(processName, out var user))
            return ResolvedCache[processName] = user;

        if (Hardcoded.TryGetValue(processName, out var hardcoded))
            return ResolvedCache[processName] = hardcoded;

        var fromRegistry = LookupRegistry(processName);
        return ResolvedCache[processName] = fromRegistry;
    }

    private static string? LookupRegistry(string processName)
    {
        try
        {
            var keyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{processName}.exe";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath)
                         ?? Registry.CurrentUser.OpenSubKey(keyPath);

            var exePath = key?.GetValue(null) as string;
            if (string.IsNullOrEmpty(exePath)) return null;

            exePath = exePath.Trim('"');
            if (!File.Exists(exePath)) return null;

            var info = FileVersionInfo.GetVersionInfo(exePath);
            var name = info.ProductName?.Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> LoadUserOverrides()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsScreenLogger",
            "friendly-names.json");

        if (!File.Exists(path))
            return new Dictionary<string, string>();

        try
        {
            var json = File.ReadAllText(path);
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return dict ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}

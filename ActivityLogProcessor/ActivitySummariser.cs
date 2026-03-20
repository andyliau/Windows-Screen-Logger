namespace ActivityLogProcessor;

public sealed record AppSummary(
    string Process,
    TimeSpan TotalDuration,
    string? FriendlyName = null);

public sealed record WindowSummary(
    string Process,
    string Title,
    TimeSpan TotalDuration);

public sealed record ActivitySummary(
    IReadOnlyList<AppSummary> ByApplication,
    IReadOnlyList<WindowSummary> TopWindows,
    TimeSpan TotalTracked);

public static class ActivitySummariser
{
    private static readonly IReadOnlyDictionary<string, string> FriendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"]    = "Visual Studio Code",
        ["devenv"]  = "Visual Studio",
        ["chrome"]  = "Google Chrome",
        ["msedge"]  = "Microsoft Edge",
        ["firefox"] = "Mozilla Firefox",
        ["teams"]   = "Microsoft Teams",
        ["slack"]   = "Slack",
        ["outlook"] = "Microsoft Outlook",
        ["explorer"]= "Windows Explorer",
    };

    public static ActivitySummary Summarise(IReadOnlyList<ActivityEntry> entries, int sampleIntervalSeconds = 5)
    {
        var byApp = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        var byWindow = new Dictionary<(string Process, string Title), TimeSpan>();

        foreach (var entry in entries)
        {
            var duration = TimeSpan.FromSeconds((entry.DotCount + 1) * sampleIntervalSeconds);
            var key = (entry.Window.Process, entry.Window.Title);

            byApp.TryGetValue(entry.Window.Process, out var existing);
            byApp[entry.Window.Process] = existing + duration;

            byWindow.TryGetValue(key, out var existingW);
            byWindow[key] = existingW + duration;
        }

        var appSummaries = byApp
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new AppSummary(
                kv.Key,
                kv.Value,
                FriendlyNames.TryGetValue(kv.Key, out var name) ? name : null))
            .ToList();

        var windowSummaries = byWindow
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new WindowSummary(kv.Key.Process, kv.Key.Title, kv.Value))
            .ToList();

        var total = byApp.Values.Aggregate(TimeSpan.Zero, (acc, d) => acc + d);

        return new ActivitySummary(appSummaries, windowSummaries, total);
    }
}

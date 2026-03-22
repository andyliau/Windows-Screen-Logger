namespace ActivityLogProcessor;

public sealed record AppSummary(
    string Process,
    TimeSpan TotalDuration,
    string? FriendlyName = null);

public sealed record WindowSummary(
    string Process,
    string Title,
    TimeSpan TotalDuration);

public sealed record TimelineEntry(
    TimeSpan Timestamp,
    string Process,
    string Title,
    TimeSpan Duration);

public sealed record ActivitySummary(
    IReadOnlyList<AppSummary> ByApplication,
    IReadOnlyList<WindowSummary> TopWindows,
    IReadOnlyList<TimelineEntry> Timeline,
    TimeSpan TotalTracked);

public static class ActivitySummariser
{
    public static ActivitySummary Summarise(
        IReadOnlyList<ActivityEntry> entries,
        int sampleIntervalSeconds = 5,
        int minTimelineSeconds = 60)
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
                FriendlyNameResolver.Resolve(kv.Key)))
            .ToList();

        var windowSummaries = byWindow
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new WindowSummary(kv.Key.Process, kv.Key.Title, kv.Value))
            .ToList();

        var timeline = entries
            .Select(e => new TimelineEntry(
                e.Window.Timestamp,
                e.Window.Process,
                e.Window.Title,
                TimeSpan.FromSeconds((e.DotCount + 1) * sampleIntervalSeconds)))
            .Where(t => t.Duration.TotalSeconds >= minTimelineSeconds)
            .ToList();

        var total = byApp.Values.Aggregate(TimeSpan.Zero, (acc, d) => acc + d);

        return new ActivitySummary(appSummaries, windowSummaries, timeline, total);
    }
}

using System.Text;
using System.Text.Json;

namespace ActivityLogProcessor;

public static class SummaryFormatter
{
    public static string FormatTimeline(ActivitySummary summary, string? dateLabel = null)
    {
        var sb = new StringBuilder();
        var label = dateLabel ?? "Activity";

        sb.AppendLine($"{label}  {FormatDuration(summary.TotalTracked)} tracked");
        sb.AppendLine();

        foreach (var entry in summary.Timeline)
        {
            var time = $"{(int)entry.Timestamp.TotalHours:D2}:{entry.Timestamp.Minutes:D2}";
            var dur = FormatMinutes(entry.Duration);
            sb.AppendLine($"{time}  {entry.Process,-14}  \"{entry.Title}\"  {dur}");
        }

        sb.AppendLine();
        var rollup = string.Join(" · ", summary.ByApplication
            .Select(a => $"{a.Process} {FormatMinutes(a.TotalDuration)}"));
        sb.Append(rollup);

        return sb.ToString();
    }

    public static string FormatText(ActivitySummary summary, string? dateLabel = null)
    {
        var sb = new StringBuilder();
        var label = dateLabel ?? "Activity Summary";

        sb.AppendLine($"Activity Summary — {label}");
        sb.AppendLine(new string('=', 30));
        sb.AppendLine();

        sb.AppendLine("By application (total time):");
        foreach (var app in summary.ByApplication)
        {
            var duration = FormatDuration(app.TotalDuration);
            var friendly = app.FriendlyName is not null ? $"  ({app.FriendlyName})" : string.Empty;
            sb.AppendLine($"  {app.Process,-14}{duration}{friendly}");
        }

        sb.AppendLine();
        sb.AppendLine("Top windows (longest focus):");
        foreach (var w in summary.TopWindows)
        {
            var mins = FormatMinutes(w.TotalDuration);
            sb.AppendLine($"  {w.Process,-8}{$"\"{w.Title}\"",-44}{mins}");
        }

        sb.AppendLine();
        sb.AppendLine($"Total tracked: {FormatDuration(summary.TotalTracked)}");

        return sb.ToString();
    }

    public static string FormatJson(ActivitySummary summary, string? dateLabel = null)
    {
        var dto = new ActivitySummaryDto(
            dateLabel,
            (long)summary.TotalTracked.TotalSeconds,
            summary.ByApplication.Select(a => new AppEntryDto(
                a.Process,
                a.FriendlyName,
                (long)a.TotalDuration.TotalSeconds)),
            summary.TopWindows.Select(w => new WindowEntryDto(
                w.Process,
                w.Title,
                (long)w.TotalDuration.TotalSeconds)));

        return JsonSerializer.Serialize(dto, JsonContext.Default.ActivitySummaryDto);
    }

    private static string FormatDuration(TimeSpan t)
        => $"{(int)t.TotalHours}h {t.Minutes:D2}m";

    private static string FormatMinutes(TimeSpan t)
        => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
            : $"{(int)t.TotalMinutes}m";
}

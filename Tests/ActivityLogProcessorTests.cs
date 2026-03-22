using System.Text.Json;
using ActivityLogProcessor;
using Xunit;

namespace WindowsScreenLogger.Tests;

public class LogParserTests
{
    [Fact]
    public void Parse_SingleWindowRecord_ReturnsOneEntry()
    {
        var lines = new[] { @"09:00:12 code ""auth.ts — VS Code""" };
        var result = LogParser.Parse(lines);

        Assert.Single(result);
        Assert.Equal("code", result[0].Window.Process);
        Assert.Equal("auth.ts — VS Code", result[0].Window.Title);
        Assert.Equal(new TimeSpan(9, 0, 12), result[0].Window.Timestamp);
        Assert.Equal(0, result[0].DotCount);
    }

    [Fact]
    public void Parse_WindowRecordWithDots_CountsDots()
    {
        var lines = new[]
        {
            @"09:00:12 code ""auth.ts — VS Code""",
            ".",
            ".",
            ".",
        };
        var result = LogParser.Parse(lines);

        Assert.Single(result);
        Assert.Equal(3, result[0].DotCount);
    }

    [Fact]
    public void Parse_MultipleWindows_AssignDotsToCorrectWindow()
    {
        var lines = new[]
        {
            @"09:00:00 code ""File A""",
            ".",
            ".",
            @"09:00:10 chrome ""GitHub""",
            ".",
        };
        var result = LogParser.Parse(lines);

        Assert.Equal(2, result.Count);
        Assert.Equal("code", result[0].Window.Process);
        Assert.Equal(2, result[0].DotCount);
        Assert.Equal("chrome", result[1].Window.Process);
        Assert.Equal(1, result[1].DotCount);
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsEmpty()
    {
        var result = LogParser.Parse(Array.Empty<string>());
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyLinesIgnored_DoesNotAffectOutput()
    {
        var lines = new[]
        {
            "",
            @"09:00:00 code ""FileA""",
            "",
            ".",
            "",
        };
        var result = LogParser.Parse(lines);

        Assert.Single(result);
        Assert.Equal(1, result[0].DotCount);
    }

    [Fact]
    public void Parse_WindowRecordWithNoDots_HasZeroDotCount()
    {
        var lines = new[]
        {
            @"09:00:00 code ""FileA""",
            @"09:00:05 chrome ""GitHub""",
        };
        var result = LogParser.Parse(lines);

        Assert.Equal(0, result[0].DotCount);
        Assert.Equal(0, result[1].DotCount);
    }

    [Fact]
    public void Parse_OrphanedDotsAtTop_AreSkipped()
    {
        // Dots before any window record should simply be ignored.
        var lines = new[]
        {
            ".",
            ".",
            @"09:00:00 code ""FileA""",
        };
        var result = LogParser.Parse(lines);

        Assert.Single(result);
        Assert.Equal(0, result[0].DotCount);
    }

    [Fact]
    public void Parse_InvalidLines_AreIgnored()
    {
        var lines = new[]
        {
            "not a valid line",
            @"09:00:00 code ""FileA""",
            "also invalid",
        };
        var result = LogParser.Parse(lines);

        Assert.Single(result);
    }
}

public class ActivitySummariserTests
{
    private static ActivityEntry MakeEntry(string proc, string title, int dots) =>
        new(new WindowRecord(TimeSpan.Zero, proc, title), dots);

    [Fact]
    public void Summarise_SingleEntry_CorrectDuration()
    {
        var entries = new[] { MakeEntry("code", "FileA", 4) };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Single(summary.ByApplication);
        Assert.Equal(TimeSpan.FromSeconds(25), summary.ByApplication[0].TotalDuration);
    }

    [Fact]
    public void Summarise_NoDots_CountsOneInterval()
    {
        var entries = new[] { MakeEntry("code", "FileA", 0) };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Equal(TimeSpan.FromSeconds(5), summary.ByApplication[0].TotalDuration);
    }

    [Fact]
    public void Summarise_GroupsByProcess()
    {
        var entries = new[]
        {
            MakeEntry("code",   "FileA", 1),
            MakeEntry("chrome", "GitHub", 1),
            MakeEntry("code",   "FileB", 3),
        };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Equal(2, summary.ByApplication.Count);
        var codeApp = summary.ByApplication.First(a => a.Process == "code");
        Assert.Equal(TimeSpan.FromSeconds(30), codeApp.TotalDuration);
    }

    [Fact]
    public void Summarise_GroupsByWindow_AccumulatesDuplicateTitles()
    {
        var entries = new[]
        {
            MakeEntry("code", "FileA", 1),
            MakeEntry("code", "FileA", 3),
        };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Single(summary.TopWindows);
        Assert.Equal(TimeSpan.FromSeconds(30), summary.TopWindows[0].TotalDuration);
    }

    [Fact]
    public void Summarise_EmptyEntries_ReturnsZeroTotal()
    {
        var summary = ActivitySummariser.Summarise(Array.Empty<ActivityEntry>(), sampleIntervalSeconds: 5);

        Assert.Empty(summary.ByApplication);
        Assert.Empty(summary.TopWindows);
        Assert.Equal(TimeSpan.Zero, summary.TotalTracked);
    }

    [Fact]
    public void Summarise_TotalTracked_IsSumOfAllApps()
    {
        var entries = new[]
        {
            MakeEntry("code",   "FileA", 1),
            MakeEntry("chrome", "GitHub", 2),
        };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Equal(TimeSpan.FromSeconds(25), summary.TotalTracked);
    }

    [Fact]
    public void Summarise_ByApplication_OrderedByDurationDescending()
    {
        var entries = new[]
        {
            MakeEntry("chrome", "GitHub", 0),
            MakeEntry("code",   "FileA",  9),
        };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Equal("code", summary.ByApplication[0].Process);
        Assert.Equal("chrome", summary.ByApplication[1].Process);
    }

    [Fact]
    public void Summarise_KnownProcess_HasFriendlyName()
    {
        var entries = new[] { MakeEntry("code", "FileA", 0) };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Equal("Visual Studio Code", summary.ByApplication[0].FriendlyName);
    }

    [Fact]
    public void Summarise_UnknownProcess_HasNullFriendlyName()
    {
        var entries = new[] { MakeEntry("totallyrandomapp99", "Untitled", 0) };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Null(summary.ByApplication[0].FriendlyName);
    }

    [Fact]
    public void Summarise_TopWindows_LimitedToTen()
    {
        var entries = Enumerable.Range(0, 15)
            .Select(i => MakeEntry("app", $"Window {i}", i))
            .ToArray();

        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5);

        Assert.Equal(10, summary.TopWindows.Count);
    }

    [Fact]
    public void Summarise_Timeline_FiltersEntriesBelowMinDuration()
    {
        var entries = new[]
        {
            MakeEntry("code",   "FileA", 11), // 12 × 5s = 60s — at threshold, included
            MakeEntry("chrome", "Quick",  0), //  1 × 5s =  5s — below threshold, excluded
            MakeEntry("code",   "FileB", 23), // 24 × 5s = 120s — included
        };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5, minTimelineSeconds: 60);

        Assert.Equal(2, summary.Timeline.Count);
        Assert.DoesNotContain(summary.Timeline, t => t.Title == "Quick");
    }

    [Fact]
    public void Summarise_Timeline_TotalsIncludeFilteredEntries()
    {
        // Short entries are hidden from the timeline but still count toward totals.
        var entries = new[]
        {
            MakeEntry("code",   "FileA",  0), //  5s — filtered from timeline
            MakeEntry("chrome", "GitHub", 11), // 60s — shown in timeline
        };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 5, minTimelineSeconds: 60);

        Assert.Single(summary.Timeline);
        Assert.Equal(2, summary.ByApplication.Count); // code still counted
        Assert.Equal(TimeSpan.FromSeconds(65), summary.TotalTracked);
    }

    [Fact]
    public void Summarise_Timeline_PreservesChronologicalOrder()
    {
        var entries = new[]
        {
            new ActivityEntry(new WindowRecord(new TimeSpan(9,  0, 0), "code",   "FileA"),   5),
            new ActivityEntry(new WindowRecord(new TimeSpan(9, 30, 0), "chrome", "GitHub"),  3),
            new ActivityEntry(new WindowRecord(new TimeSpan(9, 40, 0), "teams",  "Standup"), 7),
        };
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 60, minTimelineSeconds: 0);

        Assert.Equal(3, summary.Timeline.Count);
        Assert.Equal(new TimeSpan(9,  0, 0), summary.Timeline[0].Timestamp);
        Assert.Equal(new TimeSpan(9, 30, 0), summary.Timeline[1].Timestamp);
        Assert.Equal(new TimeSpan(9, 40, 0), summary.Timeline[2].Timestamp);
    }
}

/// <summary>
/// End-to-end acceptance tests with a realistic morning session log.
/// Read the expected values to understand exactly what the processor outputs.
///
/// Sample session (60-second sample interval):
///   09:00  code   "auth.ts — VS Code"                  30 min  (29 dots)
///   09:30  chrome "GitHub — Pull Requests"              10 min  ( 9 dots)
///   09:40  teams  "Weekly Sync — Microsoft Teams"       11 min  (10 dots)
///   09:51  code   "Program.cs — VS Code"                20 min  (19 dots)
///   10:11  chrome "Stack Overflow — async await C#"      5 min  ( 4 dots)
///   10:16  slack  "Slack — #dev channel"                 3 min  ( 2 dots)
///                                                 Total: 79 min
/// </summary>
public class SampleOutputTests
{
    private static readonly string[] SampleLog =
    [
        @"09:00:00 code ""auth.ts — VS Code""",
        .. Enumerable.Repeat(".", 29),
        @"09:30:00 chrome ""GitHub — Pull Requests""",
        .. Enumerable.Repeat(".", 9),
        @"09:40:00 teams ""Weekly Sync — Microsoft Teams""",
        .. Enumerable.Repeat(".", 10),
        @"09:51:00 code ""Program.cs — VS Code""",
        .. Enumerable.Repeat(".", 19),
        @"10:11:00 chrome ""Stack Overflow — async await C#""",
        .. Enumerable.Repeat(".", 4),
        @"10:16:00 slack ""Slack — #dev channel""",
        .. Enumerable.Repeat(".", 2),
    ];

    [Fact]
    public void FullPipeline_TimelineOutput_ShowsChronologicalFlowAndRollup()
    {
        var entries = LogParser.Parse(SampleLog);
        // minTimelineSeconds: 0 so all entries appear (all are well above 60s anyway)
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 60, minTimelineSeconds: 0);
        var text = SummaryFormatter.FormatTimeline(summary, "2024-03-20");

        // Header
        Assert.Contains("2024-03-20", text);
        Assert.Contains("1h 19m tracked", text);

        // All six timeline entries present
        Assert.Contains(@"""auth.ts — VS Code""", text);
        Assert.Contains(@"""GitHub — Pull Requests""", text);
        Assert.Contains(@"""Weekly Sync — Microsoft Teams""", text);
        Assert.Contains(@"""Program.cs — VS Code""", text);
        Assert.Contains(@"""Stack Overflow — async await C#""", text);
        Assert.Contains(@"""Slack — #dev channel""", text);

        // Entries appear in chronological order
        Assert.True(text.IndexOf("auth.ts",    StringComparison.Ordinal) <
                    text.IndexOf("Weekly Sync", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Weekly Sync", StringComparison.Ordinal) <
                    text.IndexOf("Program.cs",  StringComparison.Ordinal));

        // Compact rollup at bottom
        Assert.Contains("code 50m",   text);
        Assert.Contains("chrome 15m", text);
        Assert.Contains("teams 11m",  text);
        Assert.Contains("slack 3m",   text);
    }

    [Fact]
    public void FullPipeline_TextOutput_ShowsExpectedSummary()
    {
        var entries = LogParser.Parse(SampleLog);
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 60);
        var text = SummaryFormatter.FormatText(summary, "2024-03-20");

        Assert.Contains("Activity Summary — 2024-03-20", text);

        // By application — process padded to 14 chars, duration as Xh YYm, friendly name in parens
        Assert.Contains("code          0h 50m  (Visual Studio Code)", text);   // 30m + 20m
        Assert.Contains("chrome        0h 15m  (Google Chrome)", text);        // 10m + 5m
        Assert.Contains("teams         0h 11m  (Microsoft Teams)", text);
        Assert.Contains("slack         0h 03m  (Slack)", text);

        // Top windows — sorted by longest focus, durations < 1h shown as minutes
        Assert.Contains(@"code    ""auth.ts — VS Code""", text);       // 30m
        Assert.Contains(@"code    ""Program.cs — VS Code""", text);    // 20m
        Assert.Contains(@"teams   ""Weekly Sync — Microsoft Teams""", text); // 11m

        Assert.Contains("Total tracked: 1h 19m", text);
    }

    [Fact]
    public void FullPipeline_JsonOutput_HasExpectedStructure()
    {
        var entries = LogParser.Parse(SampleLog);
        var summary = ActivitySummariser.Summarise(entries, sampleIntervalSeconds: 60);
        var json = SummaryFormatter.FormatJson(summary, "2024-03-20");

        var root = JsonDocument.Parse(json).RootElement; // also validates well-formed JSON

        Assert.Equal("2024-03-20", root.GetProperty("date").GetString());
        Assert.Equal(4740L, root.GetProperty("totalTrackedSeconds").GetInt64()); // 79 min

        var byApp = root.GetProperty("byApplication").EnumerateArray().ToArray();
        Assert.Equal(4, byApp.Length);

        // Ranked by total time descending
        Assert.Equal("code",               byApp[0].GetProperty("process").GetString());
        Assert.Equal(3000L,                byApp[0].GetProperty("totalSeconds").GetInt64()); // 50 min
        Assert.Equal("Visual Studio Code", byApp[0].GetProperty("friendlyName").GetString());

        Assert.Equal("chrome",        byApp[1].GetProperty("process").GetString());
        Assert.Equal(900L,            byApp[1].GetProperty("totalSeconds").GetInt64()); // 15 min
        Assert.Equal("Google Chrome", byApp[1].GetProperty("friendlyName").GetString());

        Assert.Equal("teams",           byApp[2].GetProperty("process").GetString());
        Assert.Equal(660L,              byApp[2].GetProperty("totalSeconds").GetInt64()); // 11 min
        Assert.Equal("Microsoft Teams", byApp[2].GetProperty("friendlyName").GetString());

        Assert.Equal("slack", byApp[3].GetProperty("process").GetString());
        Assert.Equal(180L,    byApp[3].GetProperty("totalSeconds").GetInt64()); // 3 min
        Assert.Equal("Slack", byApp[3].GetProperty("friendlyName").GetString());

        var topWindows = root.GetProperty("topWindows").EnumerateArray().ToArray();
        Assert.Equal(6, topWindows.Length);

        Assert.Equal("auth.ts — VS Code", topWindows[0].GetProperty("title").GetString());
        Assert.Equal(1800L,               topWindows[0].GetProperty("totalSeconds").GetInt64()); // 30 min
    }
}

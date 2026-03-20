using ActivityLogProcessor;
using Xunit;

namespace ActivityLogProcessor.Tests;

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
        var entries = new[] { MakeEntry("notepad", "Untitled", 0) };
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
}

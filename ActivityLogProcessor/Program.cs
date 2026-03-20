using System.CommandLine;
using System.Text;
using System.Text.Json;
using ActivityLogProcessor;

var pathOption = new Option<FileInfo?>("--path") { Description = "Path to the .log file to process." };
var intervalOption = new Option<int>("--interval") { Description = "Sample interval in seconds." };
var outputOption = new Option<string>("--output") { Description = "Output format: summary, json, or timeline (default: summary)." };
var minDurationOption = new Option<int>("--min-duration") { Description = "Minimum window duration in seconds to show in timeline output (default: 60)." };
var outDirOption = new Option<DirectoryInfo?>("--out-dir") { Description = "Directory to write the summary file to. Falls back to activitySummaryOutputDir in WindowsScreenLogger config. Skips if file already exists." };

intervalOption.DefaultValueFactory = _ => 5;
outputOption.DefaultValueFactory = _ => "summary";
minDurationOption.DefaultValueFactory = _ => 60;

var rootCommand = new RootCommand("Processes WindowsScreenLogger activity log files.");
rootCommand.Options.Add(pathOption);
rootCommand.Options.Add(intervalOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(minDurationOption);
rootCommand.Options.Add(outDirOption);

rootCommand.SetAction((ParseResult ctx) =>
{
    var path = ctx.GetValue(pathOption);
    var interval = ctx.GetValue(intervalOption);
    var output = ctx.GetValue(outputOption) ?? "summary";
    var minDuration = ctx.GetValue(minDurationOption);
    var outDir = ctx.GetValue(outDirOption) ?? TryReadOutDirFromConfig();

    if (path is null)
    {
        Console.Error.WriteLine("Required option '--path' is missing.");
        return 1;
    }

    if (!path.Exists)
    {
        Console.Error.WriteLine($"File not found: {path.FullName}");
        return 1;
    }

    var lines = File.ReadAllLines(path.FullName);
    var entries = LogParser.Parse(lines);
    var summary = ActivitySummariser.Summarise(entries, interval, minDuration);

    var dateLabel = Path.GetFileNameWithoutExtension(path.Name);

    var result = output.ToLowerInvariant() switch
    {
        "json"     => SummaryFormatter.FormatJson(summary, dateLabel),
        "timeline" => SummaryFormatter.FormatTimeline(summary, dateLabel),
        _          => SummaryFormatter.FormatText(summary, dateLabel),
    };

    if (outDir is not null)
    {
        var ext = output.ToLowerInvariant() switch
        {
            "json"     => ".json",
            "timeline" => ".timeline.txt",
            _          => ".txt",
        };
        var outFile = Path.Combine(outDir.FullName, dateLabel + ext);

        if (File.Exists(outFile))
        {
            Console.Error.WriteLine($"Already exists, skipping: {outFile}");
            return 0;
        }

        outDir.Create();
        File.WriteAllText(outFile, result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.Error.WriteLine($"Written: {outFile}");
        return 0;
    }

    Console.WriteLine(result);
    return 0;
});

return rootCommand.Parse(args).Invoke();

static DirectoryInfo? TryReadOutDirFromConfig()
{
    var configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsScreenLogger", "config.json");

    if (!File.Exists(configPath)) return null;

    try
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        if (doc.RootElement.TryGetProperty("activitySummaryOutputDir", out var el))
        {
            var dir = el.GetString();
            if (!string.IsNullOrEmpty(dir)) return new DirectoryInfo(dir);
        }
    }
    catch { }

    return null;
}


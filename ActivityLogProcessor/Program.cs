using System.CommandLine;
using System.Text;
using System.Text.Json;
using ActivityLogProcessor;

var pathOption = new Option<FileInfo?>("--logpath") { Description = "Path to the .log file to process." };
var intervalOption = new Option<int>("--interval") { Description = "Sample interval in seconds." };
var outDirOption = new Option<DirectoryInfo?>("--out-dir") { Description = "Directory to write the summary file to. Falls back to activitySummaryOutputDir in WindowsActivityLogger config. Skips if file already exists." };

intervalOption.DefaultValueFactory = _ => 5;

var rootCommand = new RootCommand("Processes WindowsActivityLogger activity log files.");
rootCommand.Options.Add(pathOption);
rootCommand.Options.Add(intervalOption);
rootCommand.Options.Add(outDirOption);

rootCommand.SetAction((ParseResult ctx) =>
{
    var path = ctx.GetValue(pathOption);
    var interval = ctx.GetValue(intervalOption);
    var outDir = ctx.GetValue(outDirOption) ?? TryReadOutDirFromConfig();

    if (path is null)
    {
        Console.Error.WriteLine("Required option '--logpath' is missing.");
        return 1;
    }

    if (!path.Exists)
    {
        Console.Error.WriteLine($"File not found: {path.FullName}");
        return 1;
    }

    var lines = File.ReadAllLines(path.FullName);
    var entries = LogParser.Parse(lines);
    var summary = ActivitySummariser.Summarise(entries, interval);

    var dateLabel = Path.GetFileNameWithoutExtension(path.Name);

    var result = SummaryFormatter.FormatMarkdown(summary, dateLabel);

    if (outDir is not null)
    {
        var outFile = Path.Combine(outDir.FullName, dateLabel + ".md");

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
        "WindowsActivityLogger", "config.json");

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


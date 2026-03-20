using System.CommandLine;
using ActivityLogProcessor;

var pathOption = new Option<FileInfo?>("--path") { Description = "Path to the .log file to process." };
var intervalOption = new Option<int>("--interval") { Description = "Sample interval in seconds." };
var outputOption = new Option<string>("--output") { Description = "Output format: summary or json." };

intervalOption.DefaultValueFactory = _ => 5;
outputOption.DefaultValueFactory = _ => "summary";

var rootCommand = new RootCommand("Processes WindowsScreenLogger activity log files.");
rootCommand.Options.Add(pathOption);
rootCommand.Options.Add(intervalOption);
rootCommand.Options.Add(outputOption);

rootCommand.SetAction((ParseResult ctx) =>
{
    var path = ctx.GetValue(pathOption);
    var interval = ctx.GetValue(intervalOption);
    var output = ctx.GetValue(outputOption) ?? "summary";

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
    var summary = ActivitySummariser.Summarise(entries, interval);

    var dateLabel = Path.GetFileNameWithoutExtension(path.Name);

    var result = output.ToLowerInvariant() switch
    {
        "json" => SummaryFormatter.FormatJson(summary, dateLabel),
        _ => SummaryFormatter.FormatText(summary, dateLabel),
    };

    Console.WriteLine(result);
    return 0;
});

return rootCommand.Parse(args).Invoke();

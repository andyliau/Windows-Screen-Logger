using System.Text.RegularExpressions;

namespace ActivityLogProcessor;

public sealed record WindowRecord(TimeSpan Timestamp, string Process, string Title);

public sealed record ActivityEntry(WindowRecord Window, int DotCount);

public static partial class LogParser
{
    [GeneratedRegex(@"^(\d{2}):(\d{2}):(\d{2}) (\S+) ""(.*)""$")]
    private static partial Regex WindowLinePattern();

    public static IReadOnlyList<ActivityEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<ActivityEntry>();
        WindowRecord? current = null;
        int dots = 0;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (string.IsNullOrEmpty(line))
                continue;

            if (line == ".")
            {
                if (current is not null)
                    dots++;
                continue;
            }

            var match = WindowLinePattern().Match(line);
            if (!match.Success)
                continue;

            if (current is not null)
                entries.Add(new ActivityEntry(current, dots));

            current = new WindowRecord(
                new TimeSpan(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value)),
                match.Groups[4].Value,
                match.Groups[5].Value);
            dots = 0;
        }

        if (current is not null)
            entries.Add(new ActivityEntry(current, dots));

        return entries;
    }
}

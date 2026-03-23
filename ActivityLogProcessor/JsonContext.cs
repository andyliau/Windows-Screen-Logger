using System.Text.Json.Serialization;

namespace ActivityLogProcessor;

// Concrete DTOs for AOT-safe JSON serialization
public sealed record ActivitySummaryDto(
    string? Date,
    long TotalTrackedSeconds,
    IEnumerable<AppEntryDto> ByApplication,
    IEnumerable<WindowEntryDto> TopWindows);

public sealed record AppEntryDto(
    string Process,
    string? FriendlyName,
    long TotalSeconds);

public sealed record WindowEntryDto(
    string Process,
    string Title,
    long TotalSeconds);

[JsonSerializable(typeof(ActivitySummaryDto))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class JsonContext : JsonSerializerContext { }

using System.Text.Json.Serialization;

namespace WindowsScreenLogger
{
    /// <summary>
    /// One completed activity session written as a JSONL record (~250 bytes).
    /// A record is written when the user switches away from a window (or on app exit).
    /// Fields are kept minimal so AI summarisation is cheap.
    /// </summary>
    public class ActivityRecord
    {
        [JsonPropertyName("v")]
        public int Version { get; set; } = 1;

        /// <summary>ISO-8601 UTC timestamp when the user switched INTO this window.</summary>
        [JsonPropertyName("ts")]
        public string Timestamp { get; set; } = "";

        /// <summary>Seconds spent in this window before switching away.</summary>
        [JsonPropertyName("dur")]
        public int Duration { get; set; }

        /// <summary>Process name without extension (e.g. "code", "chrome").</summary>
        [JsonPropertyName("proc")]
        public string ProcessName { get; set; } = "";

        [JsonPropertyName("pid")]
        public int ProcessId { get; set; }

        /// <summary>Window title; "[redacted]" for privacy-blocked processes.</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        /// <summary>Idle seconds at the time of flush — non-zero means the user walked away.</summary>
        [JsonPropertyName("idle")]
        public int IdleSeconds { get; set; }

        /// <summary>Coarse category hint for the AI (e.g. "ide", "browser", "comms").</summary>
        [JsonPropertyName("cat")]
        public string Category { get; set; } = "";

        /// <summary>Filename of the first screenshot captured during this session.</summary>
        [JsonPropertyName("screen")]
        public string? Screenshot { get; set; }
    }
}

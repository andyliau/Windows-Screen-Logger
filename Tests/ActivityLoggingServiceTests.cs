using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using WindowsScreenLogger;
using WindowsScreenLogger.Services;
using Xunit;

namespace WindowsScreenLogger.Tests
{
    public class ActivityLoggingServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly AppConfiguration _config;
        private readonly ActivityLoggingService _sut;
        private readonly NoopLogger _logger = new();

        public ActivityLoggingServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ActivityLoggingTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);

            _config = new AppConfiguration
            {
                EnableActivityLogging = true,
                CustomSavePath = _tempDir
            };

            _sut = new ActivityLoggingService(_config, _logger);
        }

        public void Dispose()
        {
            _sut.Dispose();
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        // ── Duration tracking ────────────────────────────────────────────────

        [Fact]
        public void FirstActivity_DoesNotWriteRecord_UntilSwitch()
        {
            _sut.ProcessActivity("code", 1, "Program.cs - VS Code", "ide", 0, null);

            Assert.Empty(ReadAllRecords());
        }

        [Fact]
        public void SwitchingWindow_FlushesFirstSession_WithDuration()
        {
            _sut.ProcessActivity("code", 1, "Program.cs - VS Code", "ide", 0, null);

            // Simulate ~2 seconds passing before switching
            Thread.Sleep(2000);

            _sut.ProcessActivity("chrome", 2, "GitHub - Chrome", "browser", 0, null);

            var records = ReadAllRecords();
            Assert.Single(records);

            var r = records[0];
            Assert.Equal("code", r.ProcessName);
            Assert.Equal("Program.cs - VS Code", r.Title);
            Assert.True(r.Duration >= 1, $"Expected duration >= 1s but got {r.Duration}s");
        }

        [Fact]
        public void SameWindow_DoesNotFlush()
        {
            _sut.ProcessActivity("code", 1, "Program.cs - VS Code", "ide", 0, null);
            _sut.ProcessActivity("code", 1, "Program.cs - VS Code", "ide", 0, null);
            _sut.ProcessActivity("code", 1, "Program.cs - VS Code", "ide", 0, null);

            Assert.Empty(ReadAllRecords());
        }

        [Fact]
        public void MultipleWindows_ProduceMultipleRecordsInOrder()
        {
            _sut.ProcessActivity("code",    1, "auth.ts",    "ide",      0, "shot1.jpg");
            _sut.ProcessActivity("chrome",  2, "GitHub PR",  "browser",  0, null);
            _sut.ProcessActivity("slack",   3, "#general",   "comms",    0, null);
            _sut.Flush();

            var records = ReadAllRecords();
            Assert.Equal(3, records.Count);
            Assert.Equal("code",   records[0].ProcessName);
            Assert.Equal("chrome", records[1].ProcessName);
            Assert.Equal("slack",  records[2].ProcessName);
        }

        [Fact]
        public void Flush_WritesLastOpenSession()
        {
            _sut.ProcessActivity("code", 1, "Program.cs", "ide", 0, null);
            _sut.Flush();

            var records = ReadAllRecords();
            Assert.Single(records);
            Assert.Equal("code", records[0].ProcessName);
        }

        [Fact]
        public void Flush_AfterFlush_DoesNotWriteDuplicate()
        {
            _sut.ProcessActivity("code", 1, "Program.cs", "ide", 0, null);
            _sut.Flush();
            _sut.Flush();

            Assert.Single(ReadAllRecords());
        }

        [Fact]
        public void Duration_IsAtLeastOne()
        {
            // Even an instantaneous switch should produce duration >= 1
            _sut.ProcessActivity("code",   1, "Program.cs", "ide",     0, null);
            _sut.ProcessActivity("chrome", 2, "GitHub",     "browser", 0, null);

            var r = ReadAllRecords()[0];
            Assert.True(r.Duration >= 1);
        }

        [Fact]
        public void Screenshot_StoredOnFirstCapture_NotOnSubsequent()
        {
            _sut.ProcessActivity("code", 1, "Program.cs", "ide", 0, "shot_first.jpg");
            _sut.ProcessActivity("code", 1, "Program.cs", "ide", 0, "shot_second.jpg"); // same window
            _sut.ProcessActivity("chrome", 2, "GitHub", "browser", 0, null);

            var r = ReadAllRecords()[0];
            Assert.Equal("shot_first.jpg", r.Screenshot);
        }

        // ── Privacy filter ───────────────────────────────────────────────────

        [Fact]
        public void PrivacyFilter_BlockedProcess_HasRedactedTitle()
        {
            var filter = new PrivacyFilter();
            Assert.Equal("[redacted]", filter.FilterTitle("KeePass", "Master Password"));
            Assert.Equal("[redacted]", filter.FilterTitle("keepassxc", "My Vault"));
        }

        [Fact]
        public void PrivacyFilter_NormalProcess_PassesTitleThrough()
        {
            var filter = new PrivacyFilter();
            Assert.Equal("Program.cs - VS Code", filter.FilterTitle("code", "Program.cs - VS Code"));
        }

        // ── Category hints ───────────────────────────────────────────────────

        [Theory]
        [InlineData("devenv",   "ide")]
        [InlineData("code",     "ide")]
        [InlineData("chrome",   "browser")]
        [InlineData("msedge",   "browser")]
        [InlineData("teams",    "comms")]
        [InlineData("slack",    "comms")]
        [InlineData("spotify",  "entertainment")]
        [InlineData("unknown",  "other")]
        public void CategoryHints_ReturnsExpectedCategory(string proc, string expected)
        {
            Assert.Equal(expected, CategoryHints.Categorize(proc));
        }

        // ── JSONL serialisation ──────────────────────────────────────────────

        [Fact]
        public void Record_SerialiesesToJsonl_WithExpectedFields()
        {
            _sut.ProcessActivity("code", 42, "auth.ts - VS Code", "ide", 3, "shot.jpg");
            _sut.Flush();

            var line = File.ReadAllLines(GetLogPath())[0];
            var doc = JsonDocument.Parse(line).RootElement;

            Assert.Equal(1,            doc.GetProperty("v").GetInt32());
            Assert.Equal("code",       doc.GetProperty("proc").GetString());
            Assert.Equal(42,           doc.GetProperty("pid").GetInt32());
            Assert.Equal("auth.ts - VS Code", doc.GetProperty("title").GetString());
            Assert.Equal("ide",        doc.GetProperty("cat").GetString());
            Assert.Equal("shot.jpg",   doc.GetProperty("screen").GetString());
            Assert.True(doc.GetProperty("dur").GetInt32() >= 1);
        }

        [Fact]
        public void Record_NullScreenshot_IsOmittedFromJson()
        {
            _sut.ProcessActivity("code", 1, "auth.ts", "ide", 0, null);
            _sut.Flush();

            var line = File.ReadAllLines(GetLogPath())[0];
            var doc = JsonDocument.Parse(line).RootElement;

            Assert.False(doc.TryGetProperty("screen", out _));
        }

        [Fact]
        public void DisabledActivityLogging_WritesNoRecords()
        {
            _config.EnableActivityLogging = false;
            _sut.Capture(); // should be no-op
            _sut.Flush();

            Assert.False(File.Exists(GetLogPath()));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private List<ActivityRecord> ReadAllRecords()
        {
            var path = GetLogPath();
            if (!File.Exists(path)) return [];

            return File.ReadAllLines(path)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<ActivityRecord>(l)!)
                .ToList();
        }

        private string GetLogPath()
        {
            var dir = Path.Combine(_tempDir, DateTime.Now.ToString("yyyy-MM-dd"));
            return Path.Combine(dir, "activity.jsonl");
        }

        private sealed class NoopLogger : ILogger
        {
            public void Initialize(bool enableLogging = false, string logLevel = "Information") { }
            public void LogTrace(string message) { }
            public void LogDebug(string message) { }
            public void LogInformation(string message) { }
            public void LogWarning(string message) { }
            public void LogError(string message) { }
            public void LogCritical(string message) { }
            public void LogException(Exception ex, string? context = null) { }
            public void LogCommandLineArgs(string[] args) { }
            public void LogUninstallOperation(string operation, bool success, string? details = null) { }
            public void LogRegistryOperation(string operation, string key, bool success, string? details = null) { }
            public string? GetLogFilePath() => null;
            public void CleanupOldLogs(int daysToKeep = 7) { }
            public void LogStartup() { }
            public void LogShutdown() { }
        }
    }
}

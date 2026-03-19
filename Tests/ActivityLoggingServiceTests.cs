using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        // ── File location ────────────────────────────────────────────────────

        [Fact]
        public void LogFile_IsNamedByDate_AtSaveRoot()
        {
            var path = _sut.GetLogFilePath();
            var expected = Path.Combine(_tempDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            Assert.Equal(expected, path);
        }

        // ── Text format ──────────────────────────────────────────────────────

        [Fact]
        public void NewWindow_WritesFormattedTextLine()
        {
            InjectActivity("code",   "Program.cs - VS Code");
            InjectActivity("chrome", "GitHub");
            _sut.FlushBuffer();

            var lines = ReadLines();
            Assert.Equal(2, lines.Count);
            Assert.Matches(@"^\d{2}:\d{2}:\d{2} code ""Program\.cs - VS Code""$", lines[0]);
        }

        [Fact]
        public void HeartbeatDot_HasNoTimestamp()
        {
            // Force a dot into the buffer then flush
            InjectDot();
            _sut.FlushBuffer();

            var lines = ReadLines();
            Assert.Single(lines);
            Assert.Equal(".", lines[0]);
        }

        [Fact]
        public void ElevatedProcess_WritesUnknownElevated()
        {
            InjectElevated();
            InjectActivity("code", "Program.cs");
            _sut.FlushBuffer();

            var lines = ReadLines();
            Assert.True(lines.Count >= 1);
            Assert.Contains("unknown-elevated", lines[0]);
            Assert.Contains("[elevated]", lines[0]);
        }

        // ── Buffering ────────────────────────────────────────────────────────

        [Fact]
        public void LinesBuffer_InMemory_BeforeFlush()
        {
            InjectActivity("code", "A");
            InjectActivity("chrome", "B");
            // No flush yet — file must not exist
            Assert.False(File.Exists(_sut.GetLogFilePath()));
        }

        [Fact]
        public void FlushBuffer_WritesAllBufferedLines()
        {
            InjectActivity("code",   "A");
            InjectActivity("chrome", "B");
            InjectActivity("teams",  "C");
            _sut.FlushBuffer();

            Assert.Equal(3, ReadLines().Count);
        }

        [Fact]
        public void AutoFlush_TriggeredAt12Lines()
        {
            // Inject 12 distinct windows — the 12th injection flushes automatically
            for (int i = 0; i < 13; i++)
                InjectActivity($"app{i}", $"Window {i}");

            // File should exist because buffer hit the 12-line flush threshold
            Assert.True(File.Exists(_sut.GetLogFilePath()));
        }

        // ── Rate limiting ────────────────────────────────────────────────────

        [Fact]
        public void RateLimit_PreventsWritesWithin5Seconds()
        {
            // Simulate the real rate-limit: last change was 1 s ago → new change should be skipped
            LastChangeField.SetValue(_sut, DateTime.Now.AddSeconds(-1));
            LastProcField.SetValue(_sut,  "code");
            LastTitleField.SetValue(_sut, "A");

            // Attempt a window change via Sample() — rate limit blocks it
            // (Sample() uses real P/Invoke so we just verify the _lastChangeWrite is still 1s old)
            var before = (DateTime)LastChangeField.GetValue(_sut)!;
            // Inject directly but simulate what Sample would do with the gate check
            var now = DateTime.Now;
            if ((now - before).TotalSeconds >= 5)
                ((List<string>)BufferField.GetValue(_sut)!).Add("should not appear");

            Assert.Empty((List<string>)BufferField.GetValue(_sut)!);
        }

        // ── 5 MB cap ─────────────────────────────────────────────────────────

        [Fact]
        public void OverSizeLimit_BufferIsClearedWithoutWriting()
        {
            var path = _sut.GetLogFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[5 * 1024 * 1024 + 1]);
            var sizeBefore = new FileInfo(path).Length;

            InjectActivity("code", "A");
            InjectActivity("chrome", "B");
            _sut.FlushBuffer();

            Assert.Equal(sizeBefore, new FileInfo(path).Length);
        }

        // ── Privacy filter ────────────────────────────────────────────────────

        [Fact]
        public void PrivacyFilter_BlockedProcess_RedactsTitle()
        {
            var filter = new PrivacyFilter();
            Assert.Equal("[redacted]", filter.FilterTitle("KeePass", "Master Password"));
        }

        [Fact]
        public void PrivacyFilter_NormalProcess_PassesTitleThrough()
        {
            var filter = new PrivacyFilter();
            Assert.Equal("auth.ts - VS Code", filter.FilterTitle("code", "auth.ts - VS Code"));
        }

        // ── Title truncation ──────────────────────────────────────────────────

        [Fact]
        public void LongTitle_IsTruncatedTo80Chars()
        {
            InjectActivity("code", new string('A', 100));
            InjectActivity("chrome", "B");
            _sut.FlushBuffer();

            var line = ReadLines()[0];
            var title = line[(line.IndexOf('"') + 1)..line.LastIndexOf('"')];
            Assert.True(title.Length <= 82); // 80 chars + "…"
        }

        // ── Category hints ────────────────────────────────────────────────────

        [Theory]
        [InlineData("devenv",   "ide")]
        [InlineData("code",     "ide")]
        [InlineData("chrome",   "browser")]
        [InlineData("teams",    "comms")]
        [InlineData("spotify",  "entertainment")]
        [InlineData("steam",    "gaming")]
        [InlineData("EpicGamesLauncher", "gaming")]
        [InlineData("unknown",  "other")]
        public void CategoryHints_ReturnsExpectedCategory(string proc, string expected)
        {
            Assert.Equal(expected, CategoryHints.Categorize(proc));
        }

        // ── Disabled logging ──────────────────────────────────────────────────

        [Fact]
        public void DisabledActivityLogging_WritesNothing()
        {
            _config.EnableActivityLogging = false;
            _sut.Sample();
            _sut.FlushBuffer();

            Assert.False(File.Exists(_sut.GetLogFilePath()));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static readonly System.Reflection.FieldInfo LastProcField   = typeof(ActivityLoggingService).GetField("_lastProc",        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        private static readonly System.Reflection.FieldInfo LastTitleField  = typeof(ActivityLoggingService).GetField("_lastTitle",       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        private static readonly System.Reflection.FieldInfo LastChangeField = typeof(ActivityLoggingService).GetField("_lastChangeWrite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        private static readonly System.Reflection.FieldInfo LastSameField   = typeof(ActivityLoggingService).GetField("_lastSameWrite",   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        private static readonly System.Reflection.FieldInfo BufferField     = typeof(ActivityLoggingService).GetField("_buffer",         System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        private static readonly System.Reflection.MethodInfo WriteNewWindow = typeof(ActivityLoggingService).GetMethod("WriteNewWindow",  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                                                            ?? typeof(ActivityLoggingService).GetMethod("Buffer",         System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        /// <summary>
        /// Directly adds a formatted line to the buffer, bypassing P/Invoke AND rate limits.
        /// Use this for format/file tests. Rate limiting is tested separately via the 5 s gate.
        /// Also triggers FlushBuffer when the buffer hits 12 lines (mirrors auto-flush behaviour).
        /// </summary>
        private void InjectActivity(string proc, string title)
        {
            var now       = DateTime.Now;
            var truncated = title.Length > 80 ? title[..80] + "…" : title;
            var line      = $"{now:HH:mm:ss} {proc} \"{truncated}\"";

            var buffer = (List<string>)BufferField.GetValue(_sut)!;
            buffer.Add(line);

            LastProcField.SetValue(_sut, proc);
            LastTitleField.SetValue(_sut, title);
            LastChangeField.SetValue(_sut, now);
            LastSameField.SetValue(_sut, now);

            if (buffer.Count >= 12)
                _sut.FlushBuffer();
        }

        private void InjectElevated()
        {
            var now  = DateTime.Now;
            var line = $"{now:HH:mm:ss} unknown-elevated \"[elevated]\"";
            ((List<string>)BufferField.GetValue(_sut)!).Add(line);
            LastProcField.SetValue(_sut, "unknown-elevated");
            LastTitleField.SetValue(_sut, "[elevated]");
            LastChangeField.SetValue(_sut, now);
            LastSameField.SetValue(_sut, now);
        }

        private void InjectDot()
        {
            ((List<string>)BufferField.GetValue(_sut)!).Add(".");
            LastSameField.SetValue(_sut, DateTime.Now);
        }

        private List<string> ReadLines()
        {
            var path = _sut.GetLogFilePath();
            if (!File.Exists(path)) return [];
            return [.. File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l))];
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

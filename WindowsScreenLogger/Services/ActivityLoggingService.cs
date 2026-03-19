using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsScreenLogger.Services
{
    /// <summary>
    /// Tracks which window the user is focused on and writes a completed
    /// <see cref="ActivityRecord"/> (with duration) to a daily JSONL file each time
    /// the foreground window changes.
    ///
    /// Design:
    ///   • One record per window session, not per tick → meaningful durations
    ///   • <1 ms overhead per tick (all P/Invoke calls are fast)
    ///   • Call <see cref="Flush"/> (or <see cref="Dispose"/>) on app exit to persist
    ///     the last open session
    ///   • No new NuGet dependencies
    /// </summary>
    public class ActivityLoggingService : IDisposable
    {
        private readonly AppConfiguration _config;
        private readonly ILogger _logger;
        private readonly PrivacyFilter _privacy = new();
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Tracks the window the user is currently in.
        private sealed record ActiveSession(
            string ProcName, int Pid, string Title, string Cat,
            DateTime Start, string? Screenshot);

        private ActiveSession? _session;

        public ActivityLoggingService(AppConfiguration config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called once per capture tick. Reads the foreground window and, if the user
        /// has switched windows, flushes the previous session record with its duration.
        /// No-op when <see cref="AppConfiguration.EnableActivityLogging"/> is false.
        /// </summary>
        public void Capture(string? screenshotFilename = null)
        {
            if (!_config.EnableActivityLogging) return;

            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                var titleSb = new StringBuilder(512);
                NativeMethods.GetWindowText(hwnd, titleSb, titleSb.Capacity);
                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);

                var procName = ResolveProcessName((int)pid);
                var title = _privacy.FilterTitle(procName, titleSb.ToString());
                var idle = (int)(GetIdleMilliseconds() / 1000);

                ProcessActivity(procName, (int)pid, title,
                    CategoryHints.Categorize(procName), idle, screenshotFilename);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Activity logging");
            }
        }

        /// <summary>
        /// Core session-tracking logic. Exposed internally so tests can drive it
        /// without going through P/Invoke.
        /// </summary>
        internal void ProcessActivity(
            string procName, int pid, string title, string cat,
            int idleSeconds, string? screenshotFilename)
        {
            var now = DateTime.UtcNow;

            if (_session == null)
            {
                _session = new ActiveSession(procName, pid, title, cat, now, screenshotFilename);
                return;
            }

            // Same window — no flush needed.
            if (_session.ProcName == procName && _session.Title == title) return;

            // Window changed — close the old session with its duration.
            FlushSession(_session, end: now, idleSeconds: idleSeconds);

            _session = new ActiveSession(procName, pid, title, cat, now, screenshotFilename);
        }

        /// <summary>
        /// Writes the currently-open session to disk. Call on app exit so the last
        /// window session is not lost.
        /// </summary>
        public void Flush()
        {
            if (_session == null) return;

            var idleSeconds = 0;
            try { idleSeconds = (int)(GetIdleMilliseconds() / 1000); } catch { }

            FlushSession(_session, end: DateTime.UtcNow, idleSeconds: idleSeconds);
            _session = null;
        }

        public void Dispose() => Flush();

        private void FlushSession(ActiveSession session, DateTime end, int idleSeconds)
        {
            var record = new ActivityRecord
            {
                Timestamp = session.Start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Duration = Math.Max(1, (int)(end - session.Start).TotalSeconds),
                ProcessName = session.ProcName,
                ProcessId = session.Pid,
                Title = session.Title,
                IdleSeconds = idleSeconds,
                Category = session.Cat,
                Screenshot = session.Screenshot
            };

            Append(record);
        }

        private void Append(ActivityRecord record)
        {
            var path = GetLogPath();
            var json = JsonSerializer.Serialize(record, JsonOpts);
            File.AppendAllText(path, json + "\n");
            _logger.LogTrace($"Activity logged: {record.ProcessName} — {record.Title} ({record.Duration}s)");
        }

        private string GetLogPath()
        {
            var root = _config.GetEffectiveSavePath();
            var dir = Path.Combine(root, DateTime.Now.ToString(ApplicationConstants.ScreenshotDateFormat));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "activity.jsonl");
        }

        private static string ResolveProcessName(int pid)
        {
            try { return Process.GetProcessById(pid).ProcessName; }
            catch { return pid.ToString(); }
        }

        /// <summary>
        /// Uses unchecked uint arithmetic to handle TickCount rollover (~49-day cycle).
        /// </summary>
        private static uint GetIdleMilliseconds()
        {
            var info = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
            };
            return NativeMethods.GetLastInputInfo(ref info)
                ? unchecked((uint)Environment.TickCount - info.dwTime)
                : 0;
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsScreenLogger.Services
{
    /// <summary>
    /// Samples the foreground window every 5 s and writes activity to a daily log file
    /// in batches (one file append per minute, not per sample).
    ///
    /// Text format — one line per record, no schema:
    ///   HH:mm:ss proc "title"            — window changed or first record
    ///   .                                — same window heartbeat (no timestamp needed)
    ///
    /// Timing defaults:
    ///   • Sample interval : 5 s  (configurable via ActivitySampleIntervalSeconds)
    ///   • Buffer flush    : 60 s or when buffer reaches 12 lines, whichever comes first
    ///   • Window-change write gate : 5 s  minimum between full records
    ///   • Heartbeat (dot) gate     : 60 s minimum between dots
    ///
    /// Performance contract:
    ///   • Runs on the WinForms UI thread — no locks needed
    ///   • P/Invoke calls: <0.1 ms each
    ///   • File I/O: at most once per minute (buffered)
    ///   • Max daily file: 5 MB hard cap
    /// </summary>
    public class ActivityLoggingService : IDisposable
    {
        private const long MaxFileSizeBytes    = 5 * 1024 * 1024;
        private const int  MinChangeWriteSeconds = 5;
        private const int  SameHeartbeatSeconds  = 60;
        private const int  FlushIntervalSeconds  = 60;
        private const int  FlushLineCount        = 12;
        private const int  MaxTitleLength        = 80;

        private readonly AppConfiguration _config;
        private readonly ILogger _logger;
        private readonly PrivacyFilter _privacy = new();
        private readonly List<string> _buffer = [];

        private string? _lastProc;
        private string? _lastTitle;
        private DateTime _lastChangeWrite = DateTime.MinValue;
        private DateTime _lastSameWrite   = DateTime.MinValue;
        private DateTime _lastFlush       = DateTime.Now;
        private string?  _overSizeDateKey; // date string of the day that hit the 5 MB cap

        public ActivityLoggingService(AppConfiguration config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called on each activity timer tick (every 5 s by default).
        /// Buffers at most one line, then flushes to disk when the buffer is full
        /// (12 lines) or 60 s have elapsed since the last flush.
        /// No-op when <see cref="AppConfiguration.EnableActivityLogging"/> is false.
        /// </summary>
        public void Sample()
        {
            if (!_config.EnableActivityLogging) return;

            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                var titleSb = new System.Text.StringBuilder(512);
                NativeMethods.GetWindowText(hwnd, titleSb, titleSb.Capacity);
                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);

                var (procName, isElevated) = ResolveProcessName((int)pid);
                var rawTitle = isElevated ? "[elevated]" : titleSb.ToString();
                var title = Truncate(_privacy.FilterTitle(procName, rawTitle));

                var now = DateTime.Now;
                var windowChanged = procName != _lastProc || title != _lastTitle;

                if (windowChanged)
                {
                    if ((now - _lastChangeWrite).TotalSeconds >= MinChangeWriteSeconds)
                    {
                        Buffer($"{now:HH:mm:ss} {procName} \"{title}\"");
                        _lastProc = procName;
                        _lastTitle = title;
                        _lastChangeWrite = now;
                        _lastSameWrite = now;
                    }
                }
                else if ((now - _lastSameWrite).TotalSeconds >= SameHeartbeatSeconds)
                {
                    Buffer(".");
                    _lastSameWrite = now;
                }

                MaybeFlush(now);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Activity sampling");
            }
        }

        /// <summary>Writes any buffered lines to disk. Called automatically on schedule and on Dispose.</summary>
        public void Flush() => FlushBuffer();

        public void Dispose() => FlushBuffer();

        /// <summary>Returns the path of today's activity log file (may not yet exist).</summary>
        public string GetLogFilePath()
        {
            var root = _config.GetEffectiveSavePath();
            return Path.Combine(root, $"{DateTime.Now:yyyy-MM-dd}.log");
        }

        private void MaybeFlush(DateTime now)
        {
            if (_buffer.Count >= FlushLineCount ||
                (now - _lastFlush).TotalSeconds >= FlushIntervalSeconds)
            {
                FlushBuffer();
            }
        }

        private void Buffer(string line) => _buffer.Add(line);

        internal void FlushBuffer()
        {
            if (_buffer.Count == 0) return;

            var path = GetLogFilePath();
            if (IsOverSizeLimit(path))
            {
                _buffer.Clear();
                return;
            }

            try
            {
                Directory.CreateDirectory(_config.GetEffectiveSavePath());
                File.AppendAllLines(path, _buffer);
                _logger.LogTrace($"Activity flush: {_buffer.Count} line(s) → {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Activity log flush");
            }
            finally
            {
                _buffer.Clear();
                _lastFlush = DateTime.Now;
            }
        }

        private bool IsOverSizeLimit(string path)
        {
            if (!File.Exists(path)) return false;
            if (new FileInfo(path).Length < MaxFileSizeBytes) return false;

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_overSizeDateKey != today)
            {
                _overSizeDateKey = today;
                _logger.LogWarning("Activity log for today has reached the 5 MB limit. No further entries will be written today.");
            }
            return true;
        }

        private static (string name, bool elevated) ResolveProcessName(int pid)
        {
            try   { return (System.Diagnostics.Process.GetProcessById(pid).ProcessName, false); }
            catch { return ("unknown-elevated", true); }
        }

        private static string Truncate(string s)
            => s.Length <= MaxTitleLength ? s : s[..MaxTitleLength] + "…";
    }
}

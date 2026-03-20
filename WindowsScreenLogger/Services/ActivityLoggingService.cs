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
    ///   .                                — same window still active (one dot per sample tick)
    ///
    /// Timing defaults:
    ///   • Sample interval : 5 s  (configurable via ActivitySampleIntervalSeconds)
    ///   • Buffer flush    : 60 s or when buffer reaches 12 lines, whichever comes first
    ///   • Window-change write gate : 5 s  minimum between full records
    ///   • Each dot = one sample interval of focus time (e.g. 5 s)
    ///
    /// Performance contract:
    ///   • Runs on the WinForms UI thread — no locks needed
    ///   • P/Invoke calls: <0.1 ms each
    ///   • File I/O: at most once per minute (buffered)
    /// </summary>
    public class ActivityLoggingService : IDisposable
    {
        private const int MinChangeWriteSeconds = 5;
        private const int FlushIntervalSeconds  = 60;
        private const int FlushLineCount        = 12;
        private const int MaxTitleLength        = 80;

        private readonly AppConfiguration _config;
        private readonly ILogger _logger;
        private readonly PrivacyFilter _privacy = new();
        private readonly List<string> _buffer = [];

        private string? _lastProc;
        private string? _lastTitle;
        private DateTime _lastChangeWrite = DateTime.MinValue;
        private DateTime _lastFlush       = DateTime.Now;
        private string?  _bufferTargetPath;

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
                    }
                }
                else
                {
                    Buffer(".");
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
            // If the date has rolled over since this buffer was started, flush
            // immediately so lines timestamped "23:59:xx" go to yesterday's file,
            // not today's. Then reset last-window state so the new day's file
            // always starts with a full window record, never an orphaned dot.
            var todayPath = GetLogFilePath();
            if (_bufferTargetPath != null && _bufferTargetPath != todayPath)
            {
                FlushBuffer();
                _lastProc  = null;
                _lastTitle = null;
                return;
            }

            if (_buffer.Count >= FlushLineCount ||
                (now - _lastFlush).TotalSeconds >= FlushIntervalSeconds)
            {
                FlushBuffer();
            }
        }

        private void Buffer(string line)
        {
            // Capture the target path on the first line — ensures the whole batch
            // goes to the correct date's file even if a flush is delayed past midnight.
            _bufferTargetPath ??= GetLogFilePath();
            _buffer.Add(line);
        }

        internal void FlushBuffer()
        {
            if (_buffer.Count == 0) return;

            var path = _bufferTargetPath ?? GetLogFilePath();
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
                _bufferTargetPath = null;
                _lastFlush = DateTime.Now;
            }
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

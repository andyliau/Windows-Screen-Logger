using System.Diagnostics;

namespace WindowsScreenLogger
{
    /// <summary>
    /// Enhanced logging system for the application
    /// </summary>
    public static class AppLogger
    {
        private static string? _logFilePath;
        private static bool _isInitialized = false;
        private static LogLevel _currentLogLevel = LogLevel.Information;

        public enum LogLevel
        {
            Trace = 0,
            Debug = 1,
            Information = 2,
            Warning = 3,
            Error = 4,
            Critical = 5
        }

        /// <summary>
        /// Initializes the logging system
        /// </summary>
        public static void Initialize(bool enableLogging = false, LogLevel logLevel = LogLevel.Information)
        {
            if (_isInitialized) return;

            _currentLogLevel = logLevel;

            if (enableLogging)
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WindowsScreenLogger",
                    "Logs");

                Directory.CreateDirectory(logDirectory);

                _logFilePath = Path.Combine(logDirectory, $"WindowsScreenLogger_{DateTime.Now:yyyyMMdd}.log");
            }

            _isInitialized = true;
            LogInformation("Logging system initialized");
        }

        public static void LogTrace(string message) => Log(LogLevel.Trace, message);
        public static void LogDebug(string message) => Log(LogLevel.Debug, message);
        public static void LogInformation(string message) => Log(LogLevel.Information, message);
        public static void LogWarning(string message) => Log(LogLevel.Warning, message);
        public static void LogError(string message) => Log(LogLevel.Error, message);
        public static void LogCritical(string message) => Log(LogLevel.Critical, message);

        public static void LogException(Exception exception, string? context = null)
        {
            var message = string.IsNullOrEmpty(context) 
                ? $"Exception: {exception.Message}\nStack Trace: {exception.StackTrace}"
                : $"Exception in {context}: {exception.Message}\nStack Trace: {exception.StackTrace}";
            
            LogError(message);
        }

        /// <summary>
        /// Logs command line arguments being processed
        /// </summary>
        public static void LogCommandLineArgs(string[] args)
        {
            if (args.Length == 0)
            {
                LogDebug("No command line arguments provided");
                return;
            }

            LogInformation($"Processing {args.Length} command line arguments:");
            for (int i = 0; i < args.Length; i++)
            {
                LogInformation($"  Arg[{i}]: '{args[i]}'");
            }
        }

        /// <summary>
        /// Logs uninstall operation details
        /// </summary>
        public static void LogUninstallOperation(string operation, bool success, string? details = null)
        {
            var level = success ? LogLevel.Information : LogLevel.Error;
            var message = $"Uninstall {operation}: {(success ? "SUCCESS" : "FAILED")}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            Log(level, message);
        }

        /// <summary>
        /// Logs registry operation details
        /// </summary>
        public static void LogRegistryOperation(string operation, string key, bool success, string? details = null)
        {
            var level = success ? LogLevel.Debug : LogLevel.Warning;
            var message = $"Registry {operation} for '{key}': {(success ? "SUCCESS" : "FAILED")}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            Log(level, message);
        }

        private static void Log(LogLevel level, string message)
        {
            if (!_isInitialized || level < _currentLogLevel) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelString = level.ToString().ToUpper().PadRight(11);
            var logEntry = $"[{timestamp}] [{levelString}] {message}";

            // Always write to Debug output
            Debug.WriteLine(logEntry);

            // Write to console if available
            try
            {
                Console.WriteLine(logEntry);
            }
            catch
            {
                // Console might not be available in Windows Forms app
            }

            // Write to file if logging is enabled
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the current log file path
        /// </summary>
        public static string? GetLogFilePath() => _logFilePath;

        /// <summary>
        /// Cleans up old log files
        /// </summary>
        public static void CleanupOldLogs(int daysToKeep = 7)
        {
            if (string.IsNullOrEmpty(_logFilePath)) return;

            try
            {
                var logDirectory = Path.GetDirectoryName(_logFilePath);
                if (string.IsNullOrEmpty(logDirectory) || !Directory.Exists(logDirectory)) return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(logDirectory, "WindowsScreenLogger_*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                            LogDebug($"Deleted old log file: {logFile}");
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"Failed to delete old log file {logFile}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to cleanup old logs: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs application startup information
        /// </summary>
        public static void LogStartup()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString() ?? "Unknown";
                var location = assembly.Location;

                LogInformation("=== Windows Screen Logger Starting ===");
                LogInformation($"Version: {version}");
                LogInformation($"Location: {location}");
                LogInformation($"Process ID: {Environment.ProcessId}");
                LogInformation($"Working Directory: {Environment.CurrentDirectory}");
                LogInformation($"User: {Environment.UserName}");
                LogInformation($"Machine: {Environment.MachineName}");
                LogInformation($"OS: {Environment.OSVersion}");
                LogInformation($".NET Version: {Environment.Version}");
                LogInformation($"Is 64-bit: {Environment.Is64BitProcess}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to log startup information: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs application shutdown information
        /// </summary>
        public static void LogShutdown()
        {
            LogInformation("=== Windows Screen Logger Shutting Down ===");
        }
    }
}
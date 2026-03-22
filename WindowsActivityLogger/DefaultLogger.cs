using System.Diagnostics;

namespace WindowsActivityLogger
{
    /// <summary>
    /// Default implementation of ILogger that matches the AppLogger functionality
    /// </summary>
    public class DefaultLogger : ILogger
    {
        private string? _logFilePath;
        private bool _isInitialized = false;
        private string _currentLogLevel = "Information";

        public void Initialize(bool enableLogging = false, string logLevel = "Information")
        {
            _currentLogLevel = logLevel;

            if (enableLogging)
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ApplicationConstants.ApplicationName,
                    "Logs");

                Directory.CreateDirectory(logDirectory);

                _logFilePath = Path.Combine(logDirectory, $"{ApplicationConstants.ApplicationName}_{DateTime.Now:yyyyMMdd}.log");
            }

            _isInitialized = true;
            LogInformation("Logging system initialized");
        }

        public void LogTrace(string message) => Log("Trace", message);
        public void LogDebug(string message) => Log("Debug", message);
        public void LogInformation(string message) => Log("Information", message);
        public void LogWarning(string message) => Log("Warning", message);
        public void LogError(string message) => Log("Error", message);
        public void LogCritical(string message) => Log("Critical", message);

        public void LogException(Exception exception, string? context = null)
        {
            var message = string.IsNullOrEmpty(context)
                ? $"Exception: {exception.Message}\nStack Trace: {exception.StackTrace}"
                : $"Exception in {context}: {exception.Message}\nStack Trace: {exception.StackTrace}";

            LogError(message);
        }

        public void LogCommandLineArgs(string[] args)
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

        public void LogUninstallOperation(string operation, bool success, string? details = null)
        {
            var level = success ? "Information" : "Error";
            var message = $"Uninstall {operation}: {(success ? "SUCCESS" : "FAILED")}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            Log(level, message);
        }

        public void LogRegistryOperation(string operation, string key, bool success, string? details = null)
        {
            var level = success ? "Debug" : "Warning";
            var message = $"Registry {operation} for '{key}': {(success ? "SUCCESS" : "FAILED")}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            Log(level, message);
        }

        private void Log(string level, string message)
        {
            if (!_isInitialized || !ShouldLog(level)) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelString = level.ToUpper().PadRight(11);
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

        private bool ShouldLog(string level)
        {
            var levelOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Trace", 0 },
                { "Debug", 1 },
                { "Information", 2 },
                { "Warning", 3 },
                { "Error", 4 },
                { "Critical", 5 }
            };

            if (!levelOrder.TryGetValue(level, out var levelValue))
                return true;

            if (!levelOrder.TryGetValue(_currentLogLevel, out var currentValue))
                return true;

            return levelValue >= currentValue;
        }

        public string? GetLogFilePath() => _logFilePath;

        public void CleanupOldLogs(int daysToKeep = 7)
        {
            if (string.IsNullOrEmpty(_logFilePath)) return;

            try
            {
                var logDirectory = Path.GetDirectoryName(_logFilePath);
                if (string.IsNullOrEmpty(logDirectory) || !Directory.Exists(logDirectory)) return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(logDirectory, $"{ApplicationConstants.ApplicationName}_*.log");

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

        public void LogStartup()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString() ?? "Unknown";
                var location = assembly.Location;

                LogInformation("=== Windows Activity Logger Starting ===");
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

        public void LogShutdown()
        {
            LogInformation("=== Windows Activity Logger Shutting Down ===");
        }
    }
}

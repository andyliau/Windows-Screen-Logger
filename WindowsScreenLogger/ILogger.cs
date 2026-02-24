namespace WindowsScreenLogger
{
    /// <summary>
    /// Logger abstraction for dependency injection and testing
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Initializes the logging system
        /// </summary>
        void Initialize(bool enableLogging = false, string logLevel = "Information");

        /// <summary>
        /// Logs a trace-level message
        /// </summary>
        void LogTrace(string message);

        /// <summary>
        /// Logs a debug-level message
        /// </summary>
        void LogDebug(string message);

        /// <summary>
        /// Logs an information-level message
        /// </summary>
        void LogInformation(string message);

        /// <summary>
        /// Logs a warning-level message
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error-level message
        /// </summary>
        void LogError(string message);

        /// <summary>
        /// Logs a critical-level message
        /// </summary>
        void LogCritical(string message);

        /// <summary>
        /// Logs an exception with optional context
        /// </summary>
        void LogException(Exception exception, string? context = null);

        /// <summary>
        /// Logs command line arguments being processed
        /// </summary>
        void LogCommandLineArgs(string[] args);

        /// <summary>
        /// Logs uninstall operation details
        /// </summary>
        void LogUninstallOperation(string operation, bool success, string? details = null);

        /// <summary>
        /// Logs registry operation details
        /// </summary>
        void LogRegistryOperation(string operation, string key, bool success, string? details = null);

        /// <summary>
        /// Gets the current log file path
        /// </summary>
        string? GetLogFilePath();

        /// <summary>
        /// Cleans up old log files
        /// </summary>
        void CleanupOldLogs(int daysToKeep = 7);

        /// <summary>
        /// Logs application startup information
        /// </summary>
        void LogStartup();

        /// <summary>
        /// Logs application shutdown information
        /// </summary>
        void LogShutdown();
    }
}

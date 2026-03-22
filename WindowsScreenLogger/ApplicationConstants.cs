namespace WindowsScreenLogger
{
    /// <summary>
    /// Centralized constants for the Windows Activity Logger application.
    /// This class contains all magic numbers, default values, and configuration constants.
    /// </summary>
    public static class ApplicationConstants
    {
        /// <summary>
        /// Application name used for registry, installation paths, and logging
        /// </summary>
        public const string ApplicationName = "WindowsActivityLogger";

        /// <summary>
        /// Mutex name used to ensure only one instance runs at a time
        /// </summary>
        public const string InstanceMutexName = "WindowsActivityLoggerMutex";

        /// <summary>
        /// Default capture interval in seconds
        /// </summary>
        public const int DefaultCaptureIntervalSeconds = 5;

        /// <summary>
        /// Default image size percentage (100 = full resolution)
        /// </summary>
        public const int DefaultImageSizePercentage = 100;

        /// <summary>
        /// Default JPEG compression quality (0-100, where 100 is highest quality)
        /// </summary>
        public const int DefaultImageQuality = 30;

        /// <summary>
        /// Default number of days to keep screenshots before automatic cleanup
        /// </summary>
        public const int DefaultClearDays = 30;

        /// <summary>
        /// Default cleanup interval in hours (how often to run the cleanup task)
        /// </summary>
        public const int DefaultCleanupIntervalHours = 1;

        /// <summary>
        /// Maximum number of screenshots allowed in storage
        /// </summary>
        public const int MaxScreenshots = 1000;

        /// <summary>
        /// Default screenshot format (jpeg, png, bmp, webp)
        /// </summary>
        public const string DefaultScreenshotFormat = "jpeg";

        /// <summary>
        /// Default log level
        /// </summary>
        public const string DefaultLogLevel = "Information";

        /// <summary>
        /// Default number of old log files to keep
        /// </summary>
        public const int DefaultLogRetentionDays = 7;

        /// <summary>
        /// Registry key for Windows startup programs
        /// </summary>
        public const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Registry value name for this application's startup entry
        /// </summary>
        public const string StartupRegistryValueName = ApplicationName;

        /// <summary>
        /// Maximum number of retry attempts for mutex acquisition during installation
        /// </summary>
        public const int MutexAcquisitionMaxRetries = 5;

        /// <summary>
        /// Base delay in milliseconds for mutex acquisition retries (increases with each attempt)
        /// </summary>
        public const int MutexAcquisitionBaseDelayMs = 1000;

        /// <summary>
        /// Log file date format pattern
        /// </summary>
        public const string LogFileDateFormat = "yyyyMMdd";

        /// <summary>
        /// Screenshot filename timestamp format
        /// </summary>
        public const string ScreenshotTimestampFormat = "HHmmss";

        /// <summary>
        /// Date format for organizing screenshots
        /// </summary>
        public const string ScreenshotDateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Timeout in milliseconds for process termination
        /// </summary>
        public const int ProcessTerminationTimeoutMs = 5000;

        /// <summary>
        /// Window state for background operation (minimized and hidden)
        /// </summary>
        public const FormWindowState BackgroundWindowState = FormWindowState.Minimized;

        /// <summary>
        /// Minimum valid capture interval in seconds
        /// </summary>
        public const int MinCaptureIntervalSeconds = 1;

        /// <summary>
        /// Maximum valid capture interval in seconds
        /// </summary>
        public const int MaxCaptureIntervalSeconds = 3600; // 1 hour

        /// <summary>
        /// Minimum valid image size percentage
        /// </summary>
        public const int MinImageSizePercentage = 10;

        /// <summary>
        /// Maximum valid image size percentage
        /// </summary>
        public const int MaxImageSizePercentage = 100;

        /// <summary>
        /// Minimum valid image quality
        /// </summary>
        public const int MinImageQuality = 1;

        /// <summary>
        /// Maximum valid image quality
        /// </summary>
        public const int MaxImageQuality = 100;
    }
}

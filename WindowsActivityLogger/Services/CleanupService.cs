namespace WindowsActivityLogger.Services
{
    /// <summary>
    /// Service responsible for cleaning up old screenshots
    /// </summary>
    public class CleanupService
    {
        private readonly AppConfiguration config;
        private readonly ILogger logger;

        public CleanupService(AppConfiguration configuration, ILogger appLogger)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            logger = appLogger ?? throw new ArgumentNullException(nameof(appLogger));
        }

        /// <summary>
        /// Cleans up screenshot directories older than the configured number of days,
        /// and deletes matching daily activity log files (YYYY-MM-DD.log).
        /// </summary>
        /// <returns>Number of items deleted (directories + log files combined)</returns>
        public int CleanOldScreenshots()
        {
            var rootPath = config.GetEffectiveSavePath();
            if (!Directory.Exists(rootPath))
            {
                logger.LogDebug($"Screenshot directory does not exist: {rootPath}");
                return 0;
            }

            int deleted = 0;

            // Delete screenshot date directories
            foreach (var directory in Directory.GetDirectories(rootPath))
            {
                var creationTime = Directory.GetCreationTime(directory);
                if ((DateTime.Now - creationTime).TotalDays > config.ClearDays)
                {
                    try
                    {
                        Directory.Delete(directory, true);
                        deleted++;
                        logger.LogDebug($"Deleted old screenshot directory: {directory}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error deleting directory {directory}: {ex.Message}");
                    }
                }
            }

            // Delete activity log files (YYYY-MM-DD.log) that are beyond the retention window
            foreach (var logFile in Directory.GetFiles(rootPath, "????-??-??.log"))
            {
                var creationTime = File.GetCreationTime(logFile);
                if ((DateTime.Now - creationTime).TotalDays > config.ClearDays)
                {
                    try
                    {
                        File.Delete(logFile);
                        deleted++;
                        logger.LogDebug($"Deleted old activity log: {logFile}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error deleting activity log {logFile}: {ex.Message}");
                    }
                }
            }

            logger.LogInformation($"Cleaned up {deleted} item(s) older than {config.ClearDays} days");
            return deleted;
        }

        /// <summary>
        /// Gets the number of days screenshots are kept before cleanup
        /// </summary>
        public int GetClearDays() => config.ClearDays;

        /// <summary>
        /// Gets the cleanup interval in hours
        /// </summary>
        public int GetCleanupIntervalHours() => config.CleanupIntervalHours;
    }
}

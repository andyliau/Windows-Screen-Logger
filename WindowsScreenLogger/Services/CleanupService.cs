namespace WindowsScreenLogger.Services
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
        /// Cleans up screenshot directories older than the configured number of days
        /// </summary>
        /// <returns>Number of directories deleted</returns>
        public int CleanOldScreenshots()
        {
            var rootPath = config.GetEffectiveSavePath();
            if (!Directory.Exists(rootPath))
            {
                logger.LogDebug($"Screenshot directory does not exist: {rootPath}");
                return 0;
            }

            var subDirectories = Directory.GetDirectories(rootPath);
            int dirDeleted = 0;

            foreach (var directory in subDirectories)
            {
                var creationTime = Directory.GetCreationTime(directory);
                if ((DateTime.Now - creationTime).TotalDays > config.ClearDays)
                {
                    try
                    {
                        Directory.Delete(directory, true); // Use true to delete directories and their contents
                        dirDeleted++;
                        logger.LogDebug($"Deleted old screenshot directory: {directory}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error deleting directory {directory}: {ex.Message}");
                    }
                }
            }

            var message = $"Cleaned up {dirDeleted} screenshot folders older than {config.ClearDays} days";
            logger.LogInformation(message);

            return dirDeleted;
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

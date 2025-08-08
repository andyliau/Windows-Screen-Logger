using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsScreenLogger
{
    /// <summary>
    /// Enhanced configuration management using System.Text.Json
    /// </summary>
    public class AppConfiguration
    {
        [JsonPropertyName("captureInterval")]
        public int CaptureInterval { get; set; } = 5;

        [JsonPropertyName("imageSizePercentage")]
        public int ImageSizePercentage { get; set; } = 100;

        [JsonPropertyName("imageQuality")]
        public int ImageQuality { get; set; } = 30;

        [JsonPropertyName("clearDays")]
        public int ClearDays { get; set; } = 30;

        [JsonPropertyName("startWithWindows")]
        public bool StartWithWindows { get; set; } = false;

        [JsonPropertyName("enableLogging")]
        public bool EnableLogging { get; set; } = false;

        [JsonPropertyName("logLevel")]
        public string LogLevel { get; set; } = "Information";

        [JsonPropertyName("autoCleanup")]
        public bool AutoCleanup { get; set; } = true;

        [JsonPropertyName("cleanupInterval")]
        public int CleanupIntervalHours { get; set; } = 1;

        [JsonPropertyName("screenshotFormat")]
        public string ScreenshotFormat { get; set; } = "jpeg";

        [JsonPropertyName("maxScreenshots")]
        public int MaxScreenshots { get; set; } = 1000;

        [JsonPropertyName("customSavePath")]
        public string? CustomSavePath { get; set; }

        /// <summary>
        /// Default configuration file path
        /// </summary>
        public static string DefaultConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsScreenLogger",
            "config.json");

        /// <summary>
        /// Loads configuration from the specified file or creates default if not found
        /// </summary>
        public static AppConfiguration Load(string? configPath = null)
        {
            configPath ??= DefaultConfigPath;

            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json, GetJsonOptions());
                    return config ?? new AppConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load configuration: {ex.Message}");
            }

            // Return default configuration if loading fails
            return new AppConfiguration();
        }

        /// <summary>
        /// Saves configuration to the specified file
        /// </summary>
        public void Save(string? configPath = null)
        {
            configPath ??= DefaultConfigPath;

            try
            {
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, GetJsonOptions());
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Migrates settings from the legacy Settings.Default to the new configuration system
        /// </summary>
        public void MigrateFromLegacySettings()
        {
            try
            {
                CaptureInterval = Settings.Default.CaptureInterval;
                ImageSizePercentage = Settings.Default.ImageSizePercentage;
                ImageQuality = Settings.Default.ImageQuality;
                ClearDays = Settings.Default.ClearDays;

                // Save the migrated configuration
                Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to migrate legacy settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the configuration values and corrects any invalid settings
        /// </summary>
        public void Validate()
        {
            CaptureInterval = Math.Max(1, Math.Min(60, CaptureInterval));
            ImageSizePercentage = Math.Max(10, Math.Min(100, ImageSizePercentage));
            ImageQuality = Math.Max(10, Math.Min(100, ImageQuality));
            ClearDays = Math.Max(1, Math.Min(365, ClearDays));
            CleanupIntervalHours = Math.Max(1, Math.Min(24, CleanupIntervalHours));
            MaxScreenshots = Math.Max(10, Math.Min(10000, MaxScreenshots));

            if (!IsValidLogLevel(LogLevel))
            {
                LogLevel = "Information";
            }

            if (!IsValidScreenshotFormat(ScreenshotFormat))
            {
                ScreenshotFormat = "jpeg";
            }
        }

        private static bool IsValidLogLevel(string logLevel)
        {
            var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
            return validLevels.Contains(logLevel, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsValidScreenshotFormat(string format)
        {
            var validFormats = new[] { "jpeg", "png", "bmp", "webp" };
            return validFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
        }

        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Creates a backup of the current configuration
        /// </summary>
        public void CreateBackup(string? configPath = null)
        {
            configPath ??= DefaultConfigPath;
            var backupPath = configPath + ".backup";

            try
            {
                if (File.Exists(configPath))
                {
                    File.Copy(configPath, backupPath, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create configuration backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the effective save path for screenshots
        /// </summary>
        public string GetEffectiveSavePath()
        {
            if (!string.IsNullOrEmpty(CustomSavePath) && Directory.Exists(Path.GetDirectoryName(CustomSavePath)))
            {
                return CustomSavePath;
            }

            // Default path
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WindowsScreenLogger");
        }
    }
}
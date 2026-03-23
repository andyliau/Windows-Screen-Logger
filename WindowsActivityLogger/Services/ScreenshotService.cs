using SkiaSharp;
using System.Diagnostics;

namespace WindowsActivityLogger.Services
{
    /// <summary>
    /// Service responsible for capturing and saving screenshots
    /// </summary>
    public class ScreenshotService
    {
        private readonly AppConfiguration config;
        private readonly ILogger logger;

        public ScreenshotService(AppConfiguration configuration, ILogger appLogger)
        {
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            logger = appLogger ?? throw new ArgumentNullException(nameof(appLogger));
        }

        /// <summary>
        /// Captures all screens and saves them to the configured directory
        /// </summary>
        public void CaptureAllScreens()
        {
            var savePath = GetSavePath();
            try
            {
                // Calculate the total size of the virtual screen
                Rectangle bounds = SystemInformation.VirtualScreen;

                using var screenBitmap = new Bitmap(bounds.Width, bounds.Height);
                using var screenGraphics = Graphics.FromImage(screenBitmap);

                // Capture the entire virtual screen
                screenGraphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                // Resize the image based on settings
                int percentage = config.ImageSizePercentage;
                bool resizeNeeded = percentage != 100;
                int newWidth = bounds.Width * percentage / 100;
                int newHeight = bounds.Height * percentage / 100;

                // Convert System.Drawing.Bitmap to SkiaSharp.SKBitmap
                using var ms = new MemoryStream();
                screenBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);
                using var skBitmap = SKBitmap.Decode(ms);

                // Resize with SkiaSharp using SKSamplingOptions
                var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
                SKImage? image = null;
                try
                {
                    if (resizeNeeded)
                    {
                        using var resized = skBitmap.Resize(new SKImageInfo(newWidth, newHeight), samplingOptions);
                        image = SKImage.FromBitmap(resized);
                    }
                    else
                    {
                        image = SKImage.FromBitmap(skBitmap);
                    }

                    // Save based on configured format and quality
                    var quality = config.ImageQuality;
                    var format = config.ScreenshotFormat.ToLowerInvariant() switch
                    {
                        "png" => SKEncodedImageFormat.Png,
                        "bmp" => SKEncodedImageFormat.Bmp,
                        "webp" => SKEncodedImageFormat.Webp,
                        _ => SKEncodedImageFormat.Jpeg
                    };

                    var data = image.Encode(format, quality);
                    var extension = config.ScreenshotFormat.ToLowerInvariant() == "jpeg" ? "jpg" : config.ScreenshotFormat.ToLowerInvariant();
                    string filePath = Path.Combine(savePath, $"screenshot_{DateTime.Now:HHmmss}.{extension}");

                    using var fileStream = File.OpenWrite(filePath);
                    data.SaveTo(fileStream);

                    logger.LogTrace($"Screenshot saved: {filePath}");
                }
                finally
                {
                    image?.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogException(ex, "Screen capture");
            }
            finally
            {
                GC.Collect();
            }
        }

        /// <summary>
        /// Gets the path where screenshots should be saved
        /// </summary>
        public string GetSavePath()
        {
            var rootPath = config.GetEffectiveSavePath();
            var path = Path.Combine(rootPath, DateTime.Now.ToString(ApplicationConstants.ScreenshotDateFormat));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// Gets the root screenshot directory
        /// </summary>
        public string GetRootPath()
        {
            return config.GetEffectiveSavePath();
        }
    }
}

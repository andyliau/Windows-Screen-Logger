using WindowsScreenLogger.Installation;

namespace WindowsScreenLogger.Services
{
    /// <summary>
    /// Service for handling installation-related commands
    /// </summary>
    public class InstallationCommandService
    {
        private readonly ILogger logger;
        private bool isGuiInitialized = false;

        public InstallationCommandService(ILogger appLogger)
        {
            logger = appLogger ?? throw new ArgumentNullException(nameof(appLogger));
        }

        /// <summary>
        /// Handles the install command
        /// </summary>
        public void HandleInstallCommand(bool force = false, bool silent = false)
        {
            try
            {
                if (!silent)
                {
                    ApplicationConfiguration.Initialize();
                    isGuiInitialized = true;
                }

                if (!force && SelfInstaller.IsInstalled())
                {
                    string message = "Application is already installed.";
                    if (silent)
                    {
                        Console.WriteLine(message);
                    }
                    else
                    {
                        MessageBox.Show(message, "Already Installed",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                if (silent)
                {
                    PerformSilentInstallation();
                }
                else
                {
                    SelfInstaller.PerformInstallation();
                }

                logger.LogInformation("Installation completed successfully");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Installation failed: {ex.Message}";
                logger.LogException(ex, "Installation command");

                if (isGuiInitialized)
                {
                    MessageBox.Show(errorMessage, "Installation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Console.WriteLine(errorMessage);
                }
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Handles the uninstall command
        /// </summary>
        public void HandleUninstallCommand(bool quiet = false, bool force = false)
        {
            try
            {
                logger.LogInformation($"Starting uninstall command - quiet: {quiet}, force: {force}");

                if (!quiet)
                {
                    ApplicationConfiguration.Initialize();
                    isGuiInitialized = true;
                    logger.LogDebug("GUI initialized for uninstall command");
                }

                bool isInstalled = SelfInstaller.IsInstalled();
                logger.LogInformation($"Installation check result: {isInstalled}");

                if (!force && !isInstalled)
                {
                    string message = "Application is not installed.";
                    if (quiet)
                    {
                        Console.WriteLine(message);
                    }
                    else
                    {
                        MessageBox.Show(message, "Not Installed",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                if (quiet)
                {
                    SelfInstaller.PerformUninstallation(true);
                    Console.WriteLine("Application uninstalled successfully");
                }
                else
                {
                    SelfInstaller.PerformUninstallation(false);
                }

                logger.LogInformation("Uninstallation completed successfully");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Uninstallation failed: {ex.Message}";
                logger.LogException(ex, "Uninstall command");

                if (isGuiInitialized)
                {
                    MessageBox.Show(errorMessage, "Uninstall Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Console.WriteLine(errorMessage);
                }
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Handles the status command
        /// </summary>
        public void HandleStatusCommand()
        {
            try
            {
                var status = SelfInstaller.GetInstallationStatus();
                Console.WriteLine(status);
            }
            catch (Exception ex)
            {
                logger.LogException(ex, "Status command");
                Console.WriteLine($"Error getting status: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Performs silent installation without UI
        /// </summary>
        private void PerformSilentInstallation()
        {
            logger.LogInformation("Performing silent installation");
            SelfInstaller.PerformInstallation();
            Console.WriteLine("Installation completed successfully");
        }
    }
}

using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using WindowsScreenLogger.Installation;

namespace WindowsScreenLogger
{
    /// <summary>
    /// Enhanced command line argument handling using System.CommandLine
    /// </summary>
    public static class CommandLineHandler
    {
        private static bool isGuiInitialized = false;

        /// <summary>
        /// Creates and configures the root command with all subcommands and options
        /// </summary>
        public static RootCommand CreateRootCommand()
        {
            var rootCommand = new RootCommand("Windows Screen Logger - Automated screen capture utility");

            // Install command
            var forceOption = new Option<bool>("--force", "Force installation even if already installed");
            var silentOption = new Option<bool>("--silent", "Perform silent installation without user prompts");
            var installCommand = new Command("install", "Install the application to the user's local folder");
            installCommand.AddOption(forceOption);
            installCommand.AddOption(silentOption);
            installCommand.SetHandler((bool force, bool silent) => HandleInstallCommand(force, silent), forceOption, silentOption);

            // Uninstall command  
            var quietOption = new Option<bool>("--quiet", "Perform quiet uninstallation without user prompts");
            var uninstallForceOption = new Option<bool>("--force", "Force uninstallation even if not properly installed");
            var uninstallCommand = new Command("uninstall", "Uninstall the application");
            uninstallCommand.AddOption(quietOption);
            uninstallCommand.AddOption(uninstallForceOption);
            uninstallCommand.SetHandler((bool quiet, bool force) => HandleUninstallCommand(quiet, force), quietOption, uninstallForceOption);

            // Status command
            var statusCommand = new Command("status", "Show installation and application status");
            statusCommand.SetHandler(HandleStatusCommand);

            // Version command (alternative to --version)
            var versionCommand = new Command("version", "Show version information");
            versionCommand.SetHandler(HandleVersionCommand);

            // Run command (default behavior)
            var noInstallPromptOption = new Option<bool>("--no-install-prompt", "Skip installation prompt on first run");
            var configOption = new Option<string?>("--config", "Specify custom configuration file path");
            var runCommand = new Command("run", "Run the application (default)");
            runCommand.AddOption(noInstallPromptOption);
            runCommand.AddOption(configOption);
            runCommand.SetHandler((bool noInstallPrompt, string? configPath) => HandleRunCommand(noInstallPrompt, configPath), noInstallPromptOption, configOption);

            // Add global options
            rootCommand.AddGlobalOption(new Option<bool>("--verbose", "Enable verbose logging"));
            rootCommand.AddGlobalOption(new Option<bool>("--debug", "Enable debug mode"));
            rootCommand.AddGlobalOption(new Option<bool>("--post-install", "Internal flag for post-installation startup"));

            // Add commands
            rootCommand.AddCommand(installCommand);
            rootCommand.AddCommand(uninstallCommand);
            rootCommand.AddCommand(statusCommand);
            rootCommand.AddCommand(versionCommand);
            rootCommand.AddCommand(runCommand);

            // Set default handler for when no command is specified
            rootCommand.SetHandler((bool postInstall) => HandleDefaultCommand(postInstall),
                rootCommand.Options.OfType<Option<bool>>().First(o => o.Name == "post-install"));

            return rootCommand;
        }

        private static void HandleInstallCommand(bool force = false, bool silent = false)
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
                    // Perform silent installation
                    PerformSilentInstallation();
                }
                else
                {
                    SelfInstaller.PerformInstallation();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Installation failed: {ex.Message}";
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

        private static void HandleUninstallCommand(bool quiet = false, bool force = false)
        {
            try
            {
                if (!quiet)
                {
                    ApplicationConfiguration.Initialize();
                    isGuiInitialized = true;
                }

                if (!force && !SelfInstaller.IsInstalled())
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

                SelfInstaller.PerformUninstallation(quiet);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Uninstallation failed: {ex.Message}";
                if (!quiet && isGuiInitialized)
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

        private static void HandleStatusCommand()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString() ?? "Unknown";

                Console.WriteLine("Windows Screen Logger Status");
                Console.WriteLine("============================");
                Console.WriteLine($"Version: {version}");
                Console.WriteLine($"Current Location: {Application.ExecutablePath}");
                Console.WriteLine($"Installation Status: {SelfInstaller.GetInstallationStatus()}");
                Console.WriteLine($"Install Path: {SelfInstaller.InstallPath}");
                Console.WriteLine($"Running from Install Location: {SelfInstaller.IsRunningFromInstallLocation()}");
                Console.WriteLine($"Process Priority: {Process.GetCurrentProcess().PriorityClass}");
                Console.WriteLine($"Is Elevated: {SelfInstaller.IsElevated()}");
                
                // Startup status
                var startupStatus = StartupRegistry.IsStartupEnabled() ? "Enabled" : "Disabled";
                var startupPath = StartupRegistry.GetStartupExecutablePath();
                Console.WriteLine($"Startup Status: {startupStatus}");
                if (!string.IsNullOrEmpty(startupPath))
                {
                    Console.WriteLine($"Startup Path: {startupPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting status: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void HandleVersionCommand()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "Unknown";
            var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion ?? "Unknown";
            
            Console.WriteLine($"Windows Screen Logger v{version}");
            Console.WriteLine($"File Version: {fileVersion}");
            Console.WriteLine($"Target Framework: .NET 9.0");
            Console.WriteLine($"Copyright © 2024 WindowsScreenLogger");
        }

        private static void HandleRunCommand(bool noInstallPrompt = false, string? configPath = null)
        {
            // This will trigger the normal application startup flow
            Program.StartNormalApplication(noInstallPrompt, configPath);
        }

        private static void HandleDefaultCommand(bool postInstall = false)
        {
            // Default behavior - run the application normally
            // If this is a post-install start, pass a flag to handle mutex more gracefully
            Program.StartNormalApplication(postInstall, null, postInstall);
        }

        private static void PerformSilentInstallation()
        {
            // Create installation directory
            Directory.CreateDirectory(SelfInstaller.InstallPath);

            // Copy executable
            File.Copy(Application.ExecutablePath, SelfInstaller.InstalledExecutablePath, true);

            // Register in Windows Apps & Features
            WindowsAppsRegistry.RegisterApplication(SelfInstaller.InstallPath, SelfInstaller.InstalledExecutablePath);

            // Set startup registry entry to installed location
            StartupRegistry.SetStartupRegistration(true, SelfInstaller.InstalledExecutablePath);

            Console.WriteLine($"Windows Screen Logger installed successfully to: {SelfInstaller.InstallPath}");
        }
    }
}
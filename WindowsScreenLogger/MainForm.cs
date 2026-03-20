using System.Diagnostics;
using SkiaSharp;
using System.Drawing.Imaging;
using Microsoft.Win32;
using WindowsScreenLogger.Installation;
using WindowsScreenLogger.Services;
using Timer = System.Windows.Forms.Timer;

namespace WindowsScreenLogger;

public partial class MainForm : Form
{
	private Timer captureTimer;
	private Timer clearTimer;
	private Timer activityTimer;
	private int captureInterval;
	private ImageCodecInfo jpegEncoder;
	private bool isSessionLocked;

	private NotifyIcon notifyIcon;
	private bool isRecording = false;
	private readonly AppConfiguration config;
	private readonly ILogger _logger;
	private readonly ScreenshotService screenshotService;
	private readonly CleanupService cleanupService;
	private readonly ActivityLoggingService activityLoggingService;

	public MainForm(AppConfiguration? configuration = null, ScreenshotService? screenshot = null, CleanupService? cleanup = null, ILogger? logger = null)
	{
		config = configuration ?? AppConfiguration.Load();
		_logger = logger ?? CreateDefaultLogger(config);
		screenshotService = screenshot ?? new ScreenshotService(config, _logger);
		cleanupService = cleanup ?? new CleanupService(config, _logger);
		activityLoggingService = new ActivityLoggingService(config, _logger);
		
		InitializeComponent();
		Configure();

		SystemEvents.PowerModeChanged += OnPowerModeChanged;
		SystemEvents.SessionSwitch += OnSessionSwitch;

		// Clean up old screenshots on startup
		CleanOldScreenshots();

		 // Initialize and start the clear timer
		clearTimer = new Timer();
		clearTimer.Interval = config.CleanupIntervalHours * 60 * 60 * 1000; // Convert hours to milliseconds
		clearTimer.Tick += (sender, e) => CleanOldScreenshots();
		clearTimer.Start();

		_logger.LogInformation($"Clear timer set to {config.CleanupIntervalHours} hours");

		// Hide form on startup
		this.WindowState = FormWindowState.Minimized;
		this.ShowInTaskbar = false;
		this.Hide();
	}

	private System.ComponentModel.IContainer components = null;
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			SystemEvents.PowerModeChanged -= OnPowerModeChanged;
			SystemEvents.SessionSwitch -= OnSessionSwitch;
			captureTimer?.Stop();
			captureTimer?.Dispose();
			clearTimer?.Stop();
			clearTimer?.Dispose();
			activityTimer?.Stop();
			activityTimer?.Dispose();
			activityLoggingService.Flush(); // write any buffered lines before exit
			components?.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		components = new System.ComponentModel.Container();
		var resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
		notifyIcon = new NotifyIcon(components);
		SuspendLayout();

		Icon = (Icon)resources.GetObject("$this.Icon");

		// 
		// notifyIcon
		// 
		notifyIcon.Text = "Screen Logger";
		notifyIcon.Icon = this.Icon;
		notifyIcon.Visible = true;
		notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
		notifyIcon.ContextMenuStrip = new ContextMenuStrip();
		notifyIcon.ContextMenuStrip.Items.Add("Settings", null, ShowSettings);
		notifyIcon.ContextMenuStrip.Items.Add("Open Saved Image Folder", null, OpenSaveFolder);
		notifyIcon.ContextMenuStrip.Items.Add("Open Activity Log", null, OpenActivityLog);
		notifyIcon.ContextMenuStrip.Items.Add("Clean Old Screenshots", null, OnCleanClick);
		
		// Add uninstall option if application is installed
		if (SelfInstaller.IsInstalled() && SelfInstaller.IsRunningFromInstallLocation())
		{
			notifyIcon.ContextMenuStrip.Items.Add("-"); // Separator
			notifyIcon.ContextMenuStrip.Items.Add("Uninstall", null, OnUninstallClick);
		}
		
		notifyIcon.ContextMenuStrip.Items.Add("-"); // Separator
		notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

		// 
		// MainForm
		// 
		ClientSize = new Size(176, 0);
		Name = "MainForm";
		ShowInTaskbar = false;
		WindowState = FormWindowState.Minimized;
		Load += MainForm_Load;
		ResumeLayout(false);
	}

	private void MainForm_Load(object sender, EventArgs e)
	{
		this.Hide();
	}

	private void Configure()
	{
		// Use configuration from AppConfiguration instead of Settings.Default
		captureInterval = config.CaptureInterval;
		if (captureInterval <= 0)
		{
			_logger.LogError($"Invalid capture interval: {captureInterval}");
			MessageBox.Show("Invalid capture interval. Please check your settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}
		
		if (captureTimer != null)
		{
			captureTimer.Stop();
			captureTimer.Tick -= CaptureTimer_Tick;
		}
		
		captureTimer = new Timer
		{
			Interval = captureInterval * 1000
		};
		captureTimer.Tick += CaptureTimer_Tick;
		captureTimer.Start();
		
		_logger.LogInformation($"Capture timer configured with {captureInterval} second interval");

		// Activity logging — separate timer, independent of screenshot interval
		if (activityTimer != null)
		{
			activityTimer.Stop();
			activityTimer.Tick -= ActivityTimer_Tick;
		}

		var sampleInterval = Math.Max(1, Math.Min(30, config.ActivitySampleIntervalSeconds));
		activityTimer = new Timer { Interval = sampleInterval * 1000 };
		activityTimer.Tick += ActivityTimer_Tick;

		if (config.EnableActivityLogging)
		{
			activityTimer.Start();
			_logger.LogInformation($"Activity logging enabled — sampling every {sampleInterval}s. Log: {activityLoggingService.GetLogFilePath()}");
			ShowActivityLoggingIntroIfNeeded();
		}
		else
		{
			_logger.LogInformation("Activity logging is disabled. Enable it in Settings → Activity Logging.");
		}

		// Reflect activity logging state in the tray tooltip
		notifyIcon.Text = config.EnableActivityLogging
			? "Screen Logger (activity logging on)"
			: "Screen Logger";
	}

	private void ShowActivityLoggingIntroIfNeeded()
	{
		if (config.ActivityLoggingIntroShown) return;
		config.ActivityLoggingIntroShown = true;
		config.Save();
		Task.Delay(3000).ContinueWith(_ =>
			notifyIcon.ShowBalloonTip(
				8000,
				"Activity Logging Active",
				"Windows Screen Logger is tracking your active windows to help summarise your daily work.\n" +
				"Logs are saved alongside your screenshots. Disable anytime in Settings → Activity Logging.",
				ToolTipIcon.Info),
			TaskScheduler.FromCurrentSynchronizationContext());
	}

	private void ActivityTimer_Tick(object sender, EventArgs e)
	{
		if (!isSessionLocked)
			activityLoggingService.Sample();
	}


	private async void CaptureTimer_Tick(object sender, EventArgs e)
	{
		if (!isSessionLocked)
		{
			await CaptureAllScreensAsync();
		}
	}

	private async Task CaptureAllScreensAsync()
	{
		captureTimer.Stop();
		try
		{
			await Task.Run(() => screenshotService.CaptureAllScreens());
		}
		finally
		{
			captureTimer.Start();
		}
	}

	private void ShowSettings(object sender, EventArgs e)
	{
		using var settingsForm = new SettingsForm(config);
		settingsForm.Icon = this.Icon;
		if (settingsForm.ShowDialog() == DialogResult.OK)
		{
			Configure();
		}
	}

	private void OpenActivityLog(object sender, EventArgs e)
	{
		var logPath = activityLoggingService.GetLogFilePath();
		if (File.Exists(logPath))
		{
			Process.Start(new ProcessStartInfo("notepad.exe", logPath) { UseShellExecute = true });
		}
		else if (!config.EnableActivityLogging)
		{
			MessageBox.Show(
				"Activity logging is currently disabled.\nEnable it in Settings → Activity Logging.",
				"Activity Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		else
		{
			// Logging is on but no entries recorded yet today
			var folder = Path.GetDirectoryName(logPath)!;
			Process.Start("explorer.exe", folder);
		}
	}

	private void OpenSaveFolder(object sender, EventArgs e)
	{
		try
		{
			var savePath = screenshotService.GetSavePath();
			Process.Start("explorer.exe", savePath);
		}
		catch (Exception ex)
		{
			// Log the exception or show a message to the user
			MessageBox.Show($"An error occurred while opening the folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void Exit(object sender, EventArgs e)
	{
		if (notifyIcon != null)
		{
			notifyIcon.Visible = false;
			notifyIcon.Dispose();
		}
		Application.Exit();
	}

	private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.Suspend)
		{
			// Stop the capture timer when the system is suspended
			captureTimer?.Stop();
		}
		else if (e.Mode == PowerModes.Resume)
		{
			// Restart the capture timer when the system resumes
			captureTimer?.Start();
		}
	}

	private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
	{
		if (e.Reason == SessionSwitchReason.SessionLock)
		{
			// Set flag to indicate session is locked
			isSessionLocked = true;
		}
		else if (e.Reason == SessionSwitchReason.SessionUnlock)
		{
			// Clear flag to indicate session is unlocked
			isSessionLocked = false;
		}
	}

	private void NotifyIcon_DoubleClick(object sender, EventArgs e)
	{
		OpenSaveFolder(sender, e);
	}

	private void OnCleanClick(object? sender, EventArgs e)
	{
		int deleted = CleanOldScreenshots();
		MessageBox.Show($"Old screenshots (older than {config.ClearDays} days) have been removed. Deleted {deleted} folder(s).", 
			"Cleanup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private int CleanOldScreenshots()
	{
		return cleanupService.CleanOldScreenshots();
	}

	private static ILogger CreateDefaultLogger(AppConfiguration config)
	{
		var dl = new DefaultLogger();
		dl.Initialize(config.EnableLogging, config.LogLevel);
		return dl;
	}

	private void OnUninstallClick(object? sender, EventArgs e)	{
		// Safety check
		if (!SelfInstaller.IsInstalled() || !SelfInstaller.IsRunningFromInstallLocation())
		{
			MessageBox.Show("Uninstall is only available when running from the installed location.", 
				"Uninstall Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		var result = MessageBox.Show(
			"Are you sure you want to uninstall Windows Screen Logger?\n\n" +
			"This will:\n" +
			"� Remove the application from your system\n" +
			"� Remove it from Windows Apps & Features\n" +
			"� Disable startup with Windows\n" +
			"� Keep your screenshot files\n\n" +
			"Continue with uninstallation?",
			"Confirm Uninstall",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Question,
			MessageBoxDefaultButton.Button2);

		if (result == DialogResult.Yes)
		{
			try
			{
				SelfInstaller.PerformUninstallation();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to start uninstallation: {ex.Message}", 
					"Uninstall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}

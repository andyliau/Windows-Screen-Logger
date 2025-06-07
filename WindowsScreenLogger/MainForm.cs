using System.Diagnostics;
using SkiaSharp;
using System.Drawing.Imaging;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace WindowsScreenLogger;

public partial class MainForm : Form
{
	private Timer captureTimer;
	private Timer clearTimer;
	private int captureInterval;
	private ImageCodecInfo jpegEncoder;
	private bool isSessionLocked;

	private NotifyIcon notifyIcon;
	private bool isRecording = false;

	public MainForm()
	{
		InitializeComponent();
		Configure();

		SystemEvents.PowerModeChanged += OnPowerModeChanged;
		SystemEvents.SessionSwitch += OnSessionSwitch;

		// Clean up old screenshots on startup
		CleanOldScreenshots();

		 // Initialize and start the clear timer
		clearTimer = new Timer();
		clearTimer.Interval = 60 * 60 * 1000; // 1 hour in milliseconds
		clearTimer.Tick += (sender, e) => CleanOldScreenshots();
		clearTimer.Start();

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
		notifyIcon.ContextMenuStrip.Items.Add("Clean Old Screenshots", null, OnCleanClick);
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
		captureInterval = Settings.Default.CaptureInterval;
		if (captureInterval <= 0)
		{
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
	}


	private void CaptureTimer_Tick(object sender, EventArgs e)
	{
		if (!isSessionLocked)
		{
			CaptureAllScreens();
		}
	}

	private void CaptureAllScreens()
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
			int percentage = Settings.Default.ImageSizePercentage;
			int newWidth = bounds.Width * percentage / 100;
			int newHeight = bounds.Height * percentage / 100;

			// Convert System.Drawing.Bitmap to SkiaSharp.SKBitmap
			using var ms = new MemoryStream();
			screenBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
			ms.Seek(0, SeekOrigin.Begin);
			using var skBitmap = SKBitmap.Decode(ms);

			// Resize with SkiaSharp using SKSamplingOptions
			var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
			using var resized = skBitmap.Resize(new SKImageInfo(newWidth, newHeight), samplingOptions);
			if (resized == null)
				throw new Exception("SkiaSharp resize failed.");

			using var image = SKImage.FromBitmap(resized);

			// Save as JPEG with quality parameter
			var quality = Settings.Default.ImageQuality;
			var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

			string filePath = Path.Combine(savePath, $"screenshot_{DateTime.Now:HHmmss}.jpg");
			using var fileStream = File.OpenWrite(filePath);
			data.SaveTo(fileStream);
		}
		catch (Exception ex)
		{
			// MessageBox.Show($"An error occurred while capturing the screen: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	static string RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WindowsScreenLogger");
	static string GetSavePath()
	{
		var path = Path.Combine(
			RootPath,
			DateTime.Now.ToString("yyyy-MM-dd"));
		Directory.CreateDirectory(path);
		return path;
	}

	private void ShowSettings(object sender, EventArgs e)
	{
		using var settingsForm = new SettingsForm();
		settingsForm.Icon = this.Icon;
		if (settingsForm.ShowDialog() == DialogResult.OK)
		{
			Configure();
		}
	}

	private void OpenSaveFolder(object sender, EventArgs e)
	{
		try
		{
			var savePath = GetSavePath();
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
		CleanOldScreenshots();
		MessageBox.Show($"Old screenshots (older than {Settings.Default.ClearDays} days) have been removed.", 
			"Cleanup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void CleanOldScreenshots()
	{
		if (!Directory.Exists(RootPath))
		{
			return;
		}

		var subDirectories = Directory.GetDirectories(RootPath);
		int dirDeleted = 0;

		foreach (var directory in subDirectories)
		{
			var creationTime = Directory.GetCreationTime(directory);
			if ((DateTime.Now - creationTime).TotalDays > Settings.Default.ClearDays)
			{
				try
				{
					Directory.Delete(directory, true); // Use true to delete directories and their contents
					dirDeleted++;
				}
				catch (Exception ex)
				{
					// Log error or silently continue
					UpdateStatus($"Error deleting file {directory}: {ex.Message}");
				}
			}
		}

		UpdateStatus($"Cleaned up {dirDeleted} screenshots folders older than {Settings.Default.ClearDays} days");
	}

	private void UpdateStatus(string message)
	{
		// Update status in UI or log
		Debug.WriteLine(message);
	}
}

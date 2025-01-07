using System.Drawing.Imaging;
using System.Reflection;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace WindowsScreenLogger;

public partial class MainForm : Form
{
	private Timer captureTimer;
	private int captureInterval;
	private Bitmap screenBitmap;
	private Graphics screenGraphics;
	private ImageCodecInfo jpegEncoder;
	private string savePath;
	private bool isSessionLocked;

	private NotifyIcon notifyIcon;

	static string GetSavePath() =>
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			"WindowsScreenLogger",
			DateTime.Now.ToString("yyyy-MM-dd"));

	public MainForm()
	{
		InitializeComponent();
		ConfigureCaptureTimer();
		InitializeScreenCapture();
		jpegEncoder = GetEncoder(ImageFormat.Jpeg);
		savePath = GetSavePath();
		Directory.CreateDirectory(savePath);

		SystemEvents.PowerModeChanged += OnPowerModeChanged;
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		SystemEvents.SessionSwitch += OnSessionSwitch;
	}

	private System.ComponentModel.IContainer components = null;
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			SystemEvents.PowerModeChanged -= OnPowerModeChanged;
			SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
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
		notifyIcon.ContextMenuStrip = new ContextMenuStrip();
		notifyIcon.ContextMenuStrip.Items.Add("Open Saved Image Folder", null, OpenSaveFolder);
		notifyIcon.ContextMenuStrip.Items.Add("Settings", null, ShowSettings);
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

	private void ConfigureCaptureTimer()
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

	private void InitializeScreenCapture()
	{
		Rectangle bounds = SystemInformation.VirtualScreen;
		screenBitmap = new Bitmap(bounds.Width, bounds.Height);
		screenGraphics = Graphics.FromImage(screenBitmap);
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
		try
		{
			// Calculate the total size of the virtual screen
			Rectangle bounds = SystemInformation.VirtualScreen;

			// Capture the entire virtual screen
			screenGraphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

			// Resize the image based on settings
			int newWidth = bounds.Width;
			int newHeight = bounds.Height;
			if (Settings.Default.ImageSize != "Full")
			{
				switch (Settings.Default.ImageSize)
				{
					case "Half":
						newWidth /= 2;
						newHeight /= 2;
						break;
					case "Quarter":
						newWidth /= 4;
						newHeight /= 4;
						break;
				}
			}

			using (Bitmap resizedBitmap = new Bitmap(screenBitmap, new Size(newWidth, newHeight)))
			{
				// Reduce color depth if needed
				if (Settings.Default.ColorDepth < 32)
				{
					ReduceColorDepth(resizedBitmap, Settings.Default.ColorDepth);
				}

				// Save the resized image
				string filePath = Path.Combine(savePath, $"screenshot_{DateTime.Now:HHmmss}.jpg");
				resizedBitmap.Save(filePath, jpegEncoder, GetEncoderParameters(Settings.Default.ImageQuality));
			}
		}
		catch (Exception ex)
		{
			// Log the exception or show a message to the user
			MessageBox.Show($"An error occurred while capturing the screen: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void ReduceColorDepth(Bitmap bitmap, int colorDepth)
	{
		if (colorDepth != 8 && colorDepth != 16)
		{
			throw new ArgumentException("Color depth must be 8 or 16 bits.");
		}

		PixelFormat format = colorDepth == 8 ? PixelFormat.Format8bppIndexed : PixelFormat.Format16bppRgb565;
		Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height, format);

		using (Graphics g = Graphics.FromImage(newBitmap))
		{
			g.DrawImage(bitmap, new Rectangle(0, 0, newBitmap.Width, newBitmap.Height));
		}

		bitmap.Dispose();
		bitmap = newBitmap;
	}

	private static ImageCodecInfo GetEncoder(ImageFormat format)
	{
		ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
		foreach (ImageCodecInfo codec in codecs)
		{
			if (codec.FormatID == format.Guid)
			{
				return codec;
			}
		}
		return null;
	}

	private static EncoderParameters GetEncoderParameters(long quality)
	{
		EncoderParameters encoderParameters = new EncoderParameters(1);
		encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
		return encoderParameters;
	}

	private void ShowSettings(object sender, EventArgs e)
	{
		using var settingsForm = new SettingsForm();
		settingsForm.Icon = this.Icon;
		if (settingsForm.ShowDialog() == DialogResult.OK)
		{
			ConfigureCaptureTimer(); // Reconfigure the timer with the new interval
		}
	}

	private void OpenSaveFolder(object sender, EventArgs e)
	{
		try
		{
			if (Directory.Exists(savePath))
			{
				System.Diagnostics.Process.Start("explorer.exe", savePath);
			}
			else
			{
				MessageBox.Show("The folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
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
		screenGraphics?.Dispose();
		screenBitmap?.Dispose();
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

	private void OnDisplaySettingsChanged(object sender, EventArgs e)
	{
		// Reinitialize screen capture when display settings change
		InitializeScreenCapture();
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
}

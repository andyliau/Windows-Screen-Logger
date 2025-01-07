using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace WindowsScreenLogger;

public partial class MainForm : Form
{
	private Timer captureTimer;
	private int captureInterval;
	public static string getSavePath() => 
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			"WindowsScreenLogger", 
			DateTime.Now.ToString("yyyy-MM-dd"));

	public MainForm()
	{
		InitializeComponent();

		LoadSettings();
		ConfigureCaptureTimer();
	}

	private void MainForm_Load(object sender, EventArgs e)
	{
		this.Hide();
	}

	private void LoadSettings()
	{
		captureInterval = Settings.Default.CaptureInterval;
		if (Settings.Default.StartWithWindows)
		{
			SetStartup(true);
		}
	}

	private void ConfigureCaptureTimer()
	{
		captureTimer = new Timer
		{
			Interval = captureInterval * 1000
		};
		captureTimer.Tick += CaptureTimer_Tick;
		captureTimer.Start();
	}

	private void CaptureTimer_Tick(object sender, EventArgs e)
	{
		CaptureAllScreens();
	}

	private void CaptureAllScreens()
	{
		// Calculate the total size of the virtual screen
		Rectangle bounds = SystemInformation.VirtualScreen;
		using Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
		using Graphics g = Graphics.FromImage(bitmap);

		// Capture the entire virtual screen
		g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

		// Save the combined image
		string folderPath = getSavePath();
		Directory.CreateDirectory(folderPath);
		string filePath = Path.Combine(folderPath, $"screenshot_{DateTime.Now:HHmmss}.jpg");
		bitmap.Save(filePath, GetEncoder(ImageFormat.Jpeg), GetEncoderParameters(50L));
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
		settingsForm.ShowDialog();
	}

	private void OpenSaveFolder(object sender, EventArgs e)
	{
		string folderPath = getSavePath();
		if (Directory.Exists(folderPath))
		{
			System.Diagnostics.Process.Start("explorer.exe", folderPath);
		}
		else
		{
			MessageBox.Show("The folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void Exit(object sender, EventArgs e)
	{
		notifyIcon.Visible = false;
		Application.Exit();
	}

	private void SetStartup(bool enable)
	{
		const string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
		using RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true);
		if (enable)
		{
			key.SetValue(Application.ProductName, Application.ExecutablePath);
		}
		else
		{
			key.DeleteValue(Application.ProductName, false);
		}
	}
}

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
		captureInterval = 5; //Properties.Settings.Default.CaptureInterval;
		if (true)//Properties.Settings.Default.StartWithWindows)
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
		CaptureDesktop();
	}

	private void CaptureDesktop()
	{
		Rectangle bounds = Screen.PrimaryScreen.Bounds;
		using Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
		using Graphics g = Graphics.FromImage(bitmap);
		g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
		string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DateTime.Now.ToString("yyyy-MM-dd"));
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
		// Show settings form
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

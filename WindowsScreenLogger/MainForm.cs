using System.Drawing.Imaging;
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
		CaptureAllScreens();
	}

	private void CaptureAllScreens()
	{
		try
		{
			// Calculate the total size of the virtual screen
			Rectangle bounds = SystemInformation.VirtualScreen;

			// Capture the entire virtual screen
			screenGraphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

			// Save the combined image
			Directory.CreateDirectory(savePath);
			string filePath = Path.Combine(savePath, $"screenshot_{DateTime.Now:HHmmss}.jpg");
			screenBitmap.Save(filePath, jpegEncoder, GetEncoderParameters(50L));
		}
		catch (Exception ex)
		{
			// Log the exception or show a message to the user
			MessageBox.Show($"An error occurred while capturing the screen: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
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
}

using Microsoft.Win32;

namespace WindowsScreenLogger;

public partial class SettingsForm : Form
{
	public SettingsForm()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		this.label1 = new Label();
		this.numericUpDownInterval = new NumericUpDown();
		this.checkBoxStartWithWindows = new CheckBox();
		this.label2 = new Label();
		this.numericUpDownImageSize = new NumericUpDown();
		this.label3 = new Label();
		this.trackBarQuality = new TrackBar();
		this.buttonSave = new Button();
		this.buttonCancel = new Button();
		((System.ComponentModel.ISupportInitialize)(this.numericUpDownInterval)).BeginInit();
		((System.ComponentModel.ISupportInitialize)(this.numericUpDownImageSize)).BeginInit();
		((System.ComponentModel.ISupportInitialize)(this.trackBarQuality)).BeginInit();
		this.SuspendLayout();
		// 
		// label1
		// 
		this.label1.AutoSize = true;
		this.label1.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.label1.Location = new Point(20, 20);
		this.label1.Name = "label1";
		this.label1.Size = new Size(220, 21);
		this.label1.TabIndex = 0;
		this.label1.Text = "Recording Interval (seconds):";
		// 
		// numericUpDownInterval
		// 
		this.numericUpDownInterval.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.numericUpDownInterval.Location = new Point(250, 18);
		this.numericUpDownInterval.Minimum = 1;
		this.numericUpDownInterval.Maximum = 60;
		this.numericUpDownInterval.Name = "numericUpDownInterval";
		this.numericUpDownInterval.Size = new Size(120, 29);
		this.numericUpDownInterval.TabIndex = 1;
		this.numericUpDownInterval.Value = new decimal(new int[] { 5, 0, 0, 0 });
		// 
		// checkBoxStartWithWindows
		// 
		this.checkBoxStartWithWindows.AutoSize = true;
		this.checkBoxStartWithWindows.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.checkBoxStartWithWindows.Location = new Point(20, 60);
		this.checkBoxStartWithWindows.Name = "checkBoxStartWithWindows";
		this.checkBoxStartWithWindows.Size = new Size(160, 25);
		this.checkBoxStartWithWindows.TabIndex = 2;
		this.checkBoxStartWithWindows.Text = "Start with Windows";
		this.checkBoxStartWithWindows.UseVisualStyleBackColor = true;
		// 
		// label2
		// 
		this.label2.AutoSize = true;
		this.label2.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.label2.Location = new Point(20, 100);
		this.label2.Name = "label2";
		this.label2.Size = new Size(90, 21);
		this.label2.TabIndex = 3;
		this.label2.Text = "Image Size (%):";
		// 
		// numericUpDownImageSize
		// 
		this.numericUpDownImageSize.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.numericUpDownImageSize.Location = new Point(250, 98);
		this.numericUpDownImageSize.Minimum = 10;
		this.numericUpDownImageSize.Maximum = 100;
		this.numericUpDownImageSize.Name = "numericUpDownImageSize";
		this.numericUpDownImageSize.Size = new Size(120, 29);
		this.numericUpDownImageSize.TabIndex = 4;
		this.numericUpDownImageSize.Value = new decimal(new int[] { 100, 0, 0, 0 }); // Default to 100%
																					 // 
																					 // label3
																					 // 
		this.label3.AutoSize = true;
		this.label3.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.label3.Location = new Point(20, 140);
		this.label3.Name = "label3";
		this.label3.Size = new Size(110, 21);
		this.label3.TabIndex = 5;
		this.label3.Text = "Image Quality:";
		// 
		// trackBarQuality
		// 
		this.trackBarQuality.Location = new Point(140, 140);
		this.trackBarQuality.Minimum = 10;
		this.trackBarQuality.Maximum = 100;
		this.trackBarQuality.TickFrequency = 10;
		this.trackBarQuality.Value = 30; // Default to 30
		this.trackBarQuality.Name = "trackBarQuality";
		this.trackBarQuality.Size = new Size(230, 45);
		this.trackBarQuality.TabIndex = 6;
		// 
		// buttonSave
		// 
		this.buttonSave.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.buttonSave.Location = new Point(100, 200);
		this.buttonSave.Name = "buttonSave";
		this.buttonSave.Size = new Size(100, 30);
		this.buttonSave.TabIndex = 7;
		this.buttonSave.Text = "Save";
		this.buttonSave.UseVisualStyleBackColor = true;
		this.buttonSave.Click += new EventHandler(this.ButtonSave_Click);
		// 
		// buttonCancel
		// 
		this.buttonCancel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.buttonCancel.Location = new Point(220, 200);
		this.buttonCancel.Name = "buttonCancel";
		this.buttonCancel.Size = new Size(100, 30);
		this.buttonCancel.TabIndex = 8;
		this.buttonCancel.Text = "Cancel";
		this.buttonCancel.UseVisualStyleBackColor = true;
		this.buttonCancel.Click += new EventHandler(this.ButtonCancel_Click);
		// 
		// SettingsForm
		// 
		this.AutoScaleDimensions = new SizeF(7F, 15F);
		this.AutoScaleMode = AutoScaleMode.Font;
		this.ClientSize = new Size(400, 250);
		this.Controls.Add(this.buttonCancel);
		this.Controls.Add(this.buttonSave);
		this.Controls.Add(this.trackBarQuality);
		this.Controls.Add(this.label3);
		this.Controls.Add(this.numericUpDownImageSize);
		this.Controls.Add(this.label2);
		this.Controls.Add(this.checkBoxStartWithWindows);
		this.Controls.Add(this.numericUpDownInterval);
		this.Controls.Add(this.label1);
		this.FormBorderStyle = FormBorderStyle.FixedDialog;
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.Name = "SettingsForm";
		this.StartPosition = FormStartPosition.CenterScreen;
		this.Text = "Settings";
		this.Load += new EventHandler(this.SettingsForm_Load);
		((System.ComponentModel.ISupportInitialize)(this.numericUpDownInterval)).EndInit();
		((System.ComponentModel.ISupportInitialize)(this.numericUpDownImageSize)).EndInit();
		((System.ComponentModel.ISupportInitialize)(this.trackBarQuality)).EndInit();
		this.ResumeLayout(false);
		this.PerformLayout();
	}

	private Label label1;
	private NumericUpDown numericUpDownInterval;
	private CheckBox checkBoxStartWithWindows;
	private Label label2;
	private NumericUpDown numericUpDownImageSize;
	private Label label3;
	private TrackBar trackBarQuality;
	private Button buttonSave;
	private Button buttonCancel;

	private void SettingsForm_Load(object sender, EventArgs e)
	{
		// Load settings
		numericUpDownInterval.Value = Settings.Default.CaptureInterval;
		checkBoxStartWithWindows.Checked = GetStartup();
		numericUpDownImageSize.Value = Settings.Default.ImageSizePercentage;
		trackBarQuality.Value = Settings.Default.ImageQuality;
	}

	private void ButtonSave_Click(object sender, EventArgs e)
	{
		// Save settings
		Settings.Default.CaptureInterval = (int)numericUpDownInterval.Value;
		Settings.Default.ImageSizePercentage = (int)numericUpDownImageSize.Value;
		Settings.Default.ImageQuality = trackBarQuality.Value;
		Settings.Default.Save();

		// Set or remove startup entry
		SetStartup(checkBoxStartWithWindows.Checked);

		this.DialogResult = DialogResult.OK;
		this.Close();
	}

	private void ButtonCancel_Click(object sender, EventArgs e)
	{
		this.DialogResult = DialogResult.Cancel;
		this.Close();
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

	private bool GetStartup()
	{
		const string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
		using RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true);
		return key.GetValue(Application.ProductName) != null;
	}
}

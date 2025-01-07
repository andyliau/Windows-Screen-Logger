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
		this.comboBoxImageSize = new ComboBox();
		this.label3 = new Label();
		this.trackBarQuality = new TrackBar();
		this.label4 = new Label();
		this.comboBoxColorDepth = new ComboBox();
		this.buttonSave = new Button();
		this.buttonCancel = new Button();
		((System.ComponentModel.ISupportInitialize)(this.numericUpDownInterval)).BeginInit();
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
		this.label2.Text = "Image Size:";
		// 
		// comboBoxImageSize
		// 
		this.comboBoxImageSize.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.comboBoxImageSize.FormattingEnabled = true;
		this.comboBoxImageSize.Items.AddRange(new object[] {
				"Full",
				"Half",
				"Quarter"});
		this.comboBoxImageSize.Location = new Point(120, 97);
		this.comboBoxImageSize.Name = "comboBoxImageSize";
		this.comboBoxImageSize.Size = new Size(120, 29);
		this.comboBoxImageSize.TabIndex = 4;
		this.comboBoxImageSize.SelectedIndex = 0; // Default to "Full"
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
		// label4
		// 
		this.label4.AutoSize = true;
		this.label4.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.label4.Location = new Point(20, 190);
		this.label4.Name = "label4";
		this.label4.Size = new Size(90, 21);
		this.label4.TabIndex = 7;
		this.label4.Text = "Color Depth:";
		// 
		// comboBoxColorDepth
		// 
		this.comboBoxColorDepth.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.comboBoxColorDepth.FormattingEnabled = true;
		this.comboBoxColorDepth.Items.AddRange(new object[] {
				"32",
				"16",
				"8"});
		this.comboBoxColorDepth.Location = new Point(120, 187);
		this.comboBoxColorDepth.Name = "comboBoxColorDepth";
		this.comboBoxColorDepth.Size = new Size(120, 29);
		this.comboBoxColorDepth.TabIndex = 8;
		this.comboBoxColorDepth.SelectedIndex = 0; // Default to "32"
												   // 
												   // buttonSave
												   // 
		this.buttonSave.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.buttonSave.Location = new Point(100, 240);
		this.buttonSave.Name = "buttonSave";
		this.buttonSave.Size = new Size(100, 30);
		this.buttonSave.TabIndex = 9;
		this.buttonSave.Text = "Save";
		this.buttonSave.UseVisualStyleBackColor = true;
		this.buttonSave.Click += new EventHandler(this.ButtonSave_Click);
		// 
		// buttonCancel
		// 
		this.buttonCancel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.buttonCancel.Location = new Point(220, 240);
		this.buttonCancel.Name = "buttonCancel";
		this.buttonCancel.Size = new Size(100, 30);
		this.buttonCancel.TabIndex = 10;
		this.buttonCancel.Text = "Cancel";
		this.buttonCancel.UseVisualStyleBackColor = true;
		this.buttonCancel.Click += new EventHandler(this.ButtonCancel_Click);
		// 
		// SettingsForm
		// 
		this.AutoScaleDimensions = new SizeF(7F, 15F);
		this.AutoScaleMode = AutoScaleMode.Font;
		this.ClientSize = new Size(400, 300);
		this.Controls.Add(this.buttonCancel);
		this.Controls.Add(this.buttonSave);
		this.Controls.Add(this.comboBoxColorDepth);
		this.Controls.Add(this.label4);
		this.Controls.Add(this.trackBarQuality);
		this.Controls.Add(this.label3);
		this.Controls.Add(this.comboBoxImageSize);
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
		((System.ComponentModel.ISupportInitialize)(this.trackBarQuality)).EndInit();
		this.ResumeLayout(false);
		this.PerformLayout();
	}

	private Label label1;
	private NumericUpDown numericUpDownInterval;
	private CheckBox checkBoxStartWithWindows;
	private Label label2;
	private ComboBox comboBoxImageSize;
	private Label label3;
	private TrackBar trackBarQuality;
	private Label label4;
	private ComboBox comboBoxColorDepth;
	private Button buttonSave;
	private Button buttonCancel;

	private void SettingsForm_Load(object sender, EventArgs e)
	{
		// Load settings
		numericUpDownInterval.Value = Settings.Default.CaptureInterval;
		checkBoxStartWithWindows.Checked = GetStartup();
		comboBoxImageSize.SelectedItem = Settings.Default.ImageSize;
		trackBarQuality.Value = Settings.Default.ImageQuality;
		comboBoxColorDepth.SelectedItem = Settings.Default.ColorDepth.ToString();
	}

	private void ButtonSave_Click(object sender, EventArgs e)
	{
		// Save settings
		Settings.Default.CaptureInterval = (int)numericUpDownInterval.Value;
		Settings.Default.ImageSize = comboBoxImageSize.SelectedItem.ToString();
		Settings.Default.ImageQuality = trackBarQuality.Value;
		Settings.Default.ColorDepth = int.Parse(comboBoxColorDepth.SelectedItem.ToString());
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

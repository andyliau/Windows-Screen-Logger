using System;
using System.Windows.Forms;
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
		this.buttonSave = new Button();
		this.buttonCancel = new Button();
		((System.ComponentModel.ISupportInitialize)(this.numericUpDownInterval)).BeginInit();
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
		this.numericUpDownInterval.Minimum = new decimal(new int[] {
			1,
			0,
			0,
			0});
		this.numericUpDownInterval.Name = "numericUpDownInterval";
		this.numericUpDownInterval.Size = new Size(120, 29);
		this.numericUpDownInterval.TabIndex = 1;
		this.numericUpDownInterval.Value = new decimal(new int[] {
			5,
			0,
			0,
			0});
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
		// buttonSave
		// 
		this.buttonSave.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.buttonSave.Location = new Point(100, 100);
		this.buttonSave.Name = "buttonSave";
		this.buttonSave.Size = new Size(100, 30);
		this.buttonSave.TabIndex = 3;
		this.buttonSave.Text = "Save";
		this.buttonSave.UseVisualStyleBackColor = true;
		this.buttonSave.Click += new EventHandler(this.ButtonSave_Click);
		// 
		// buttonCancel
		// 
		this.buttonCancel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
		this.buttonCancel.Location = new Point(220, 100);
		this.buttonCancel.Name = "buttonCancel";
		this.buttonCancel.Size = new Size(100, 30);
		this.buttonCancel.TabIndex = 4;
		this.buttonCancel.Text = "Cancel";
		this.buttonCancel.UseVisualStyleBackColor = true;
		this.buttonCancel.Click += new EventHandler(this.ButtonCancel_Click);
		// 
		// SettingsForm
		// 
		this.AutoScaleDimensions = new SizeF(7F, 15F);
		this.AutoScaleMode = AutoScaleMode.Font;
		this.ClientSize = new Size(400, 150);
		this.Controls.Add(this.buttonCancel);
		this.Controls.Add(this.buttonSave);
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
		this.ResumeLayout(false);
		this.PerformLayout();
	}

	private Label label1;
	private NumericUpDown numericUpDownInterval;
	private CheckBox checkBoxStartWithWindows;
	private Button buttonSave;
	private Button buttonCancel;

	private void SettingsForm_Load(object sender, EventArgs e)
	{
		// Load settings
		numericUpDownInterval.Value = Settings.Default.CaptureInterval;
		checkBoxStartWithWindows.Checked = Settings.Default.StartWithWindows;
	}

	private void ButtonSave_Click(object sender, EventArgs e)
	{
		// Save settings
		Settings.Default.CaptureInterval = (int)numericUpDownInterval.Value;
		Settings.Default.StartWithWindows = checkBoxStartWithWindows.Checked;
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
}
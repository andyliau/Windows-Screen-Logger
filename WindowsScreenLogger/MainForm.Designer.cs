namespace WindowsScreenLogger
{
	partial class MainForm
	{
		/// <summary>
		///  Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		///  Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		///  Required method for Designer support - do not modify
		///  the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new System.ComponentModel.Container();
			notifyIcon = new NotifyIcon(components);
			SuspendLayout();
			// 
			// notifyIcon
			// 
			notifyIcon.Text = "Screen Logger";
			notifyIcon.Icon = SystemIcons.Application;
			notifyIcon.Visible = true;
			notifyIcon.ContextMenuStrip = new ContextMenuStrip();
			notifyIcon.ContextMenuStrip.Items.Add("Open Saved Image Folder", null, OpenSaveFolder);
			notifyIcon.ContextMenuStrip.Items.Add("Settings", null, ShowSettings);
			notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

			// 
			// MainForm
			// 
			this.ClientSize = new Size(0, 0);
			this.Name = "MainForm";
			this.WindowState = FormWindowState.Minimized;
			this.ShowInTaskbar = false;
			this.Load += new EventHandler(this.MainForm_Load);
			this.ResumeLayout(false);
		}

		#endregion

		private NotifyIcon notifyIcon;
	}
}

namespace cdeWin
{
	partial class LoaderForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
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
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoaderForm));
			this.tbLog = new System.Windows.Forms.TextBox();
			this.btnOK = new System.Windows.Forms.Button();
			this.cbAutoCloseLoader = new System.Windows.Forms.CheckBox();
			this.pictureCDEIcon = new System.Windows.Forms.PictureBox();
			((System.ComponentModel.ISupportInitialize)(this.pictureCDEIcon)).BeginInit();
			this.SuspendLayout();
			// 
			// tbLog
			// 
			this.tbLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tbLog.Location = new System.Drawing.Point(13, 12);
			this.tbLog.Multiline = true;
			this.tbLog.Name = "tbLog";
			this.tbLog.Size = new System.Drawing.Size(409, 150);
			this.tbLog.TabIndex = 0;
			// 
			// btnOK
			// 
			this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnOK.Location = new System.Drawing.Point(347, 209);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(75, 23);
			this.btnOK.TabIndex = 1;
			this.btnOK.Text = "OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
			// 
			// cbAutoCloseLoader
			// 
			this.cbAutoCloseLoader.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.cbAutoCloseLoader.AutoSize = true;
			this.cbAutoCloseLoader.Location = new System.Drawing.Point(103, 213);
			this.cbAutoCloseLoader.Name = "cbAutoCloseLoader";
			this.cbAutoCloseLoader.Size = new System.Drawing.Size(238, 17);
			this.cbAutoCloseLoader.TabIndex = 2;
			this.cbAutoCloseLoader.Text = "Autoclose Loader Window on load complete.";
			this.cbAutoCloseLoader.UseVisualStyleBackColor = true;
			// 
			// pictureCDEIcon
			// 
			this.pictureCDEIcon.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.pictureCDEIcon.Image = global::cdeWin.Properties.Resources.CDE_logo_02;
			this.pictureCDEIcon.Location = new System.Drawing.Point(13, 168);
			this.pictureCDEIcon.Name = "pictureCDEIcon";
			this.pictureCDEIcon.Size = new System.Drawing.Size(64, 64);
			this.pictureCDEIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.pictureCDEIcon.TabIndex = 4;
			this.pictureCDEIcon.TabStop = false;
			// 
			// LoaderForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(434, 244);
			this.Controls.Add(this.pictureCDEIcon);
			this.Controls.Add(this.cbAutoCloseLoader);
			this.Controls.Add(this.btnOK);
			this.Controls.Add(this.tbLog);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "LoaderForm";
			this.Text = "Catalog Loader";
			this.Shown += new System.EventHandler(this.LoaderForm_Shown);
			((System.ComponentModel.ISupportInitialize)(this.pictureCDEIcon)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox tbLog;
		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.CheckBox cbAutoCloseLoader;
		private System.Windows.Forms.PictureBox pictureCDEIcon;
	}
}
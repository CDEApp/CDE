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
			this.pictureCDEIcon = new System.Windows.Forms.PictureBox();
			this.barLoading = new System.Windows.Forms.ProgressBar();
			this.lblProgressMessage = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this.pictureCDEIcon)).BeginInit();
			this.SuspendLayout();
			// 
			// pictureCDEIcon
			// 
			// this.pictureCDEIcon.Image = global::cdeWin.Properties.Resources.CDE_logo_02;
			this.pictureCDEIcon.Location = new System.Drawing.Point(12, 12);
			this.pictureCDEIcon.Name = "pictureCDEIcon";
			this.pictureCDEIcon.Size = new System.Drawing.Size(64, 64);
			this.pictureCDEIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.pictureCDEIcon.TabIndex = 4;
			this.pictureCDEIcon.TabStop = false;
			// 
			// barLoading
			// 
			this.barLoading.Location = new System.Drawing.Point(82, 24);
			this.barLoading.Name = "barLoading";
			this.barLoading.Size = new System.Drawing.Size(207, 39);
			this.barLoading.TabIndex = 5;
			// 
			// lblProgressMessage
			// 
			this.lblProgressMessage.AutoSize = true;
			this.lblProgressMessage.Location = new System.Drawing.Point(82, 76);
			this.lblProgressMessage.Name = "lblProgressMessage";
			this.lblProgressMessage.Size = new System.Drawing.Size(35, 13);
			this.lblProgressMessage.TabIndex = 6;
			this.lblProgressMessage.Text = "label1";
			// 
			// LoaderForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(302, 99);
			this.ControlBox = false;
			this.Controls.Add(this.lblProgressMessage);
			this.Controls.Add(this.barLoading);
			this.Controls.Add(this.pictureCDEIcon);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "LoaderForm";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.Text = "Catalog Loader";
			this.Shown += new System.EventHandler(this.LoaderForm_Shown);
			((System.ComponentModel.ISupportInitialize)(this.pictureCDEIcon)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.PictureBox pictureCDEIcon;
		private System.Windows.Forms.ProgressBar barLoading;
		private System.Windows.Forms.Label lblProgressMessage;
	}
}
namespace cdeWin
{
    partial class MyAboutBox
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MyAboutBox));
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.okButton = new System.Windows.Forms.Button();
            this.topPanel = new System.Windows.Forms.Panel();
            this.pictureCDEIcon = new System.Windows.Forms.PictureBox();
            this.pictureHeronLogo = new System.Windows.Forms.PictureBox();
            this.messageTextBox = new System.Windows.Forms.TextBox();
            this.bottomPanel.SuspendLayout();
            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureCDEIcon)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureHeronLogo)).BeginInit();
            this.SuspendLayout();
            // 
            // bottomPanel
            // 
            this.bottomPanel.Controls.Add(this.okButton);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 381);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(294, 42);
            this.bottomPanel.TabIndex = 4;
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(207, 10);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 0;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.OkButtonClick);
            // 
            // topPanel
            // 
            this.topPanel.BackColor = System.Drawing.Color.White;
            this.topPanel.Controls.Add(this.pictureCDEIcon);
            this.topPanel.Controls.Add(this.pictureHeronLogo);
            this.topPanel.Controls.Add(this.messageTextBox);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Size = new System.Drawing.Size(294, 381);
            this.topPanel.TabIndex = 5;
            // 
            // pictureCDEIcon
            // 
            this.pictureCDEIcon.Image = global::cdeWin.Properties.Resources.CDE_logo_02;
            this.pictureCDEIcon.Location = new System.Drawing.Point(218, 234);
            this.pictureCDEIcon.Name = "pictureCDEIcon";
            this.pictureCDEIcon.Size = new System.Drawing.Size(64, 64);
            this.pictureCDEIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.pictureCDEIcon.TabIndex = 3;
            this.pictureCDEIcon.TabStop = false;
            // 
            // pictureHeronLogo
            // 
            this.pictureHeronLogo.Image = global::cdeWin.Properties.Resources.heron_200_black;
            this.pictureHeronLogo.Location = new System.Drawing.Point(12, 12);
            this.pictureHeronLogo.Name = "pictureHeronLogo";
            this.pictureHeronLogo.Size = new System.Drawing.Size(200, 286);
            this.pictureHeronLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.pictureHeronLogo.TabIndex = 2;
            this.pictureHeronLogo.TabStop = false;
            // 
            // messageTextBox
            // 
            this.messageTextBox.BackColor = System.Drawing.Color.White;
            this.messageTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.messageTextBox.Location = new System.Drawing.Point(12, 304);
            this.messageTextBox.Multiline = true;
            this.messageTextBox.Name = "messageTextBox";
            this.messageTextBox.ReadOnly = true;
            this.messageTextBox.Size = new System.Drawing.Size(270, 64);
            this.messageTextBox.TabIndex = 1;
            this.messageTextBox.Text = "cdeWin vX.XX\r\nThis is a viewer for catalog files created with \"cde\".\r\nYou can con" +
    "tact me at rob@somedomain.com\r\n";
            // 
            // MyAboutBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(294, 423);
            this.Controls.Add(this.topPanel);
            this.Controls.Add(this.bottomPanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MyAboutBox";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About cdeWin";
            this.bottomPanel.ResumeLayout(false);
            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureCDEIcon)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureHeronLogo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.TextBox messageTextBox;
        private System.Windows.Forms.PictureBox pictureHeronLogo;
        private System.Windows.Forms.PictureBox pictureCDEIcon;
    }
}
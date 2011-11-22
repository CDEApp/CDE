namespace cdeWin
{
    partial class DisplayTreeFromRootFormForm
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
            this.tvMain = new System.Windows.Forms.TreeView();
            this.btnLoadData = new System.Windows.Forms.Button();
            this.lvEntries = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // tvMain
            // 
            this.tvMain.HideSelection = false;
            this.tvMain.Location = new System.Drawing.Point(12, 12);
            this.tvMain.Name = "tvMain";
            this.tvMain.Size = new System.Drawing.Size(349, 471);
            this.tvMain.TabIndex = 0;
            // 
            // btnLoadData
            // 
            this.btnLoadData.Location = new System.Drawing.Point(12, 489);
            this.btnLoadData.Name = "btnLoadData";
            this.btnLoadData.Size = new System.Drawing.Size(75, 23);
            this.btnLoadData.TabIndex = 1;
            this.btnLoadData.Text = "Load Data";
            this.btnLoadData.UseVisualStyleBackColor = true;
            // 
            // lvEntries
            // 
            this.lvEntries.Location = new System.Drawing.Point(367, 12);
            this.lvEntries.Name = "lvEntries";
            this.lvEntries.Size = new System.Drawing.Size(276, 471);
            this.lvEntries.TabIndex = 2;
            this.lvEntries.UseCompatibleStateImageBehavior = false;
            // 
            // DisplayTreeFromRootFormForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(655, 625);
            this.Controls.Add(this.lvEntries);
            this.Controls.Add(this.btnLoadData);
            this.Controls.Add(this.tvMain);
            this.Name = "DisplayTreeFromRootFormForm";
            this.Text = "DisplayTreeFromRootFormForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView tvMain;
        private System.Windows.Forms.Button btnLoadData;
        private System.Windows.Forms.ListView lvEntries;
    }
}


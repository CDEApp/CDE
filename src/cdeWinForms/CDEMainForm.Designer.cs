using System;
using System.Collections.Generic;
using cdeLib;
using cdeWinFormsPresenter;

namespace cdeWinForms
{
    partial class CDEMainForm : ICDEMainView
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
            this.button1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(36, 31);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(117, 33);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 20);
            this.textBox1.TabIndex = 1;
            // 
            // CDEMainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.button1);
            this.Name = "CDEMainForm";
            this.Text = "CDEMainForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox textBox1;

        private ICDEMainPresenterCallBacks _presenter;
        public void Attach(ICDEMainPresenterCallBacks presenter)
        {
            _presenter = presenter;
            //button1.Click += delegate(object sender, EventArgs e) { _presenter.OnButtonClick(); }; // not using lambda
            button1.Click += (sender, e) => _presenter.OnButtonClick();
        }

        public bool ButtonEnabled
        {
            get { return button1.Enabled; }
            set { button1.Enabled = value; }
        }

        public string ButtonText
        {
            get { return button1.Text; }
            set { button1.Text = value; }
        }

        public IList<RootEntry> GetCatalogs()
        {
            throw new NotImplementedException();
        }
    }
}


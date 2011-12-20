namespace cdeWin
{
    partial class CDEWinForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CDEWinForm));
            this.directoryTreeView = new System.Windows.Forms.TreeView();
            this.directoryListView = new System.Windows.Forms.ListView();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mainTabControl = new System.Windows.Forms.TabControl();
            this.searchTab = new System.Windows.Forms.TabPage();
            this.searchResultPanel = new System.Windows.Forms.Panel();
            this.searchResultListView = new System.Windows.Forms.ListView();
            this.searchControlPanel = new System.Windows.Forms.Panel();
            this.patternTextBox = new System.Windows.Forms.TextBox();
            this.labelSearchPath = new System.Windows.Forms.Label();
            this.whatToSearchComboBox = new System.Windows.Forms.ComboBox();
            this.checkboxFolders = new System.Windows.Forms.CheckBox();
            this.checkboxFiles = new System.Windows.Forms.CheckBox();
            this.searchButton = new System.Windows.Forms.Button();
            this.regexCheckbox = new System.Windows.Forms.CheckBox();
            this.searchLabel = new System.Windows.Forms.Label();
            this.catalogTab = new System.Windows.Forms.TabPage();
            this.catalogResultPanel = new System.Windows.Forms.Panel();
            this.catalogResultListView = new System.Windows.Forms.ListView();
            this.catalogControlPanel = new System.Windows.Forms.Panel();
            this.labelCatalogPlaceholder = new System.Windows.Forms.Label();
            this.directoryTab = new System.Windows.Forms.TabPage();
            this.directorySplitContainer = new System.Windows.Forms.SplitContainer();
            this.directoryBottomPanel = new System.Windows.Forms.Panel();
            this.copyPathButton = new System.Windows.Forms.Button();
            this.directoryPathTextBox = new System.Windows.Forms.TextBox();
            this.mainStatusStrip = new System.Windows.Forms.StatusStrip();
            this.catalogsLoadedStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.searchResultsStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelRightAlign = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
            this.searchTimeStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1.SuspendLayout();
            this.mainTabControl.SuspendLayout();
            this.searchTab.SuspendLayout();
            this.searchResultPanel.SuspendLayout();
            this.searchControlPanel.SuspendLayout();
            this.catalogTab.SuspendLayout();
            this.catalogResultPanel.SuspendLayout();
            this.catalogControlPanel.SuspendLayout();
            this.directoryTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.directorySplitContainer)).BeginInit();
            this.directorySplitContainer.Panel1.SuspendLayout();
            this.directorySplitContainer.Panel2.SuspendLayout();
            this.directorySplitContainer.SuspendLayout();
            this.directoryBottomPanel.SuspendLayout();
            this.mainStatusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // directoryTreeView
            // 
            this.directoryTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directoryTreeView.HideSelection = false;
            this.directoryTreeView.Location = new System.Drawing.Point(0, 0);
            this.directoryTreeView.Name = "directoryTreeView";
            this.directoryTreeView.Size = new System.Drawing.Size(179, 393);
            this.directoryTreeView.TabIndex = 0;
            // 
            // directoryListView
            // 
            this.directoryListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directoryListView.Location = new System.Drawing.Point(0, 0);
            this.directoryListView.Name = "directoryListView";
            this.directoryListView.Size = new System.Drawing.Size(355, 393);
            this.directoryListView.TabIndex = 2;
            this.directoryListView.UseCompatibleStateImageBehavior = false;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(552, 24);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(92, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            // 
            // mainTabControl
            // 
            this.mainTabControl.Controls.Add(this.searchTab);
            this.mainTabControl.Controls.Add(this.catalogTab);
            this.mainTabControl.Controls.Add(this.directoryTab);
            this.mainTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTabControl.Location = new System.Drawing.Point(0, 24);
            this.mainTabControl.Name = "mainTabControl";
            this.mainTabControl.SelectedIndex = 0;
            this.mainTabControl.Size = new System.Drawing.Size(552, 452);
            this.mainTabControl.TabIndex = 6;
            // 
            // searchTab
            // 
            this.searchTab.Controls.Add(this.searchResultPanel);
            this.searchTab.Controls.Add(this.searchControlPanel);
            this.searchTab.Location = new System.Drawing.Point(4, 22);
            this.searchTab.Name = "searchTab";
            this.searchTab.Padding = new System.Windows.Forms.Padding(3);
            this.searchTab.Size = new System.Drawing.Size(544, 426);
            this.searchTab.TabIndex = 0;
            this.searchTab.Text = "Search";
            this.searchTab.UseVisualStyleBackColor = true;
            // 
            // searchResultPanel
            // 
            this.searchResultPanel.Controls.Add(this.searchResultListView);
            this.searchResultPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchResultPanel.Location = new System.Drawing.Point(3, 71);
            this.searchResultPanel.Name = "searchResultPanel";
            this.searchResultPanel.Size = new System.Drawing.Size(538, 352);
            this.searchResultPanel.TabIndex = 1;
            // 
            // searchResultListView
            // 
            this.searchResultListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchResultListView.Location = new System.Drawing.Point(0, 0);
            this.searchResultListView.Name = "searchResultListView";
            this.searchResultListView.Size = new System.Drawing.Size(538, 352);
            this.searchResultListView.TabIndex = 0;
            this.searchResultListView.UseCompatibleStateImageBehavior = false;
            // 
            // searchControlPanel
            // 
            this.searchControlPanel.Controls.Add(this.patternTextBox);
            this.searchControlPanel.Controls.Add(this.labelSearchPath);
            this.searchControlPanel.Controls.Add(this.whatToSearchComboBox);
            this.searchControlPanel.Controls.Add(this.checkboxFolders);
            this.searchControlPanel.Controls.Add(this.checkboxFiles);
            this.searchControlPanel.Controls.Add(this.searchButton);
            this.searchControlPanel.Controls.Add(this.regexCheckbox);
            this.searchControlPanel.Controls.Add(this.searchLabel);
            this.searchControlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchControlPanel.Location = new System.Drawing.Point(3, 3);
            this.searchControlPanel.Name = "searchControlPanel";
            this.searchControlPanel.Size = new System.Drawing.Size(538, 68);
            this.searchControlPanel.TabIndex = 0;
            // 
            // patternTextBox
            // 
            this.patternTextBox.Location = new System.Drawing.Point(87, 2);
            this.patternTextBox.Name = "patternTextBox";
            this.patternTextBox.Size = new System.Drawing.Size(253, 20);
            this.patternTextBox.TabIndex = 8;
            // 
            // labelSearchPath
            // 
            this.labelSearchPath.AutoSize = true;
            this.labelSearchPath.Location = new System.Drawing.Point(6, 28);
            this.labelSearchPath.Name = "labelSearchPath";
            this.labelSearchPath.Size = new System.Drawing.Size(66, 13);
            this.labelSearchPath.TabIndex = 7;
            this.labelSearchPath.Text = "Search Path";
            // 
            // whatToSearchComboBox
            // 
            this.whatToSearchComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.whatToSearchComboBox.FormattingEnabled = true;
            this.whatToSearchComboBox.Location = new System.Drawing.Point(87, 25);
            this.whatToSearchComboBox.Name = "whatToSearchComboBox";
            this.whatToSearchComboBox.Size = new System.Drawing.Size(80, 21);
            this.whatToSearchComboBox.TabIndex = 6;
            // 
            // checkboxFolders
            // 
            this.checkboxFolders.AutoSize = true;
            this.checkboxFolders.Checked = true;
            this.checkboxFolders.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkboxFolders.Location = new System.Drawing.Point(346, 45);
            this.checkboxFolders.Name = "checkboxFolders";
            this.checkboxFolders.Size = new System.Drawing.Size(60, 17);
            this.checkboxFolders.TabIndex = 5;
            this.checkboxFolders.Text = "Folders";
            this.checkboxFolders.UseVisualStyleBackColor = true;
            // 
            // checkboxFiles
            // 
            this.checkboxFiles.AutoSize = true;
            this.checkboxFiles.Checked = true;
            this.checkboxFiles.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkboxFiles.Location = new System.Drawing.Point(346, 24);
            this.checkboxFiles.Name = "checkboxFiles";
            this.checkboxFiles.Size = new System.Drawing.Size(47, 17);
            this.checkboxFiles.TabIndex = 4;
            this.checkboxFiles.Text = "Files";
            this.checkboxFiles.UseVisualStyleBackColor = true;
            // 
            // searchButton
            // 
            this.searchButton.Location = new System.Drawing.Point(458, -1);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(75, 23);
            this.searchButton.TabIndex = 3;
            this.searchButton.Text = "&Search";
            this.searchButton.UseVisualStyleBackColor = true;
            // 
            // regexCheckbox
            // 
            this.regexCheckbox.AutoSize = true;
            this.regexCheckbox.Location = new System.Drawing.Point(346, 4);
            this.regexCheckbox.Name = "regexCheckbox";
            this.regexCheckbox.Size = new System.Drawing.Size(57, 17);
            this.regexCheckbox.TabIndex = 2;
            this.regexCheckbox.Text = "Rege&x";
            this.regexCheckbox.UseVisualStyleBackColor = true;
            // 
            // searchLabel
            // 
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(31, 4);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(41, 13);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "&Pattern";
            // 
            // catalogTab
            // 
            this.catalogTab.Controls.Add(this.catalogResultPanel);
            this.catalogTab.Controls.Add(this.catalogControlPanel);
            this.catalogTab.Location = new System.Drawing.Point(4, 22);
            this.catalogTab.Name = "catalogTab";
            this.catalogTab.Padding = new System.Windows.Forms.Padding(3);
            this.catalogTab.Size = new System.Drawing.Size(544, 426);
            this.catalogTab.TabIndex = 1;
            this.catalogTab.Text = "Catalog";
            this.catalogTab.UseVisualStyleBackColor = true;
            // 
            // catalogResultPanel
            // 
            this.catalogResultPanel.Controls.Add(this.catalogResultListView);
            this.catalogResultPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.catalogResultPanel.Location = new System.Drawing.Point(3, 26);
            this.catalogResultPanel.Name = "catalogResultPanel";
            this.catalogResultPanel.Size = new System.Drawing.Size(538, 397);
            this.catalogResultPanel.TabIndex = 1;
            // 
            // catalogResultListView
            // 
            this.catalogResultListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.catalogResultListView.Location = new System.Drawing.Point(0, 0);
            this.catalogResultListView.Name = "catalogResultListView";
            this.catalogResultListView.Size = new System.Drawing.Size(538, 397);
            this.catalogResultListView.TabIndex = 0;
            this.catalogResultListView.UseCompatibleStateImageBehavior = false;
            // 
            // catalogControlPanel
            // 
            this.catalogControlPanel.Controls.Add(this.labelCatalogPlaceholder);
            this.catalogControlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.catalogControlPanel.Location = new System.Drawing.Point(3, 3);
            this.catalogControlPanel.Name = "catalogControlPanel";
            this.catalogControlPanel.Size = new System.Drawing.Size(538, 23);
            this.catalogControlPanel.TabIndex = 0;
            // 
            // labelCatalogPlaceholder
            // 
            this.labelCatalogPlaceholder.AutoSize = true;
            this.labelCatalogPlaceholder.Location = new System.Drawing.Point(4, 4);
            this.labelCatalogPlaceholder.Name = "labelCatalogPlaceholder";
            this.labelCatalogPlaceholder.Size = new System.Drawing.Size(229, 13);
            this.labelCatalogPlaceholder.TabIndex = 0;
            this.labelCatalogPlaceholder.Text = "Place to put create catalog controls eventually.";
            // 
            // directoryTab
            // 
            this.directoryTab.Controls.Add(this.directorySplitContainer);
            this.directoryTab.Controls.Add(this.directoryBottomPanel);
            this.directoryTab.Location = new System.Drawing.Point(4, 22);
            this.directoryTab.Name = "directoryTab";
            this.directoryTab.Padding = new System.Windows.Forms.Padding(3);
            this.directoryTab.Size = new System.Drawing.Size(544, 426);
            this.directoryTab.TabIndex = 2;
            this.directoryTab.Text = "Directory";
            this.directoryTab.UseVisualStyleBackColor = true;
            // 
            // directorySplitContainer
            // 
            this.directorySplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directorySplitContainer.Location = new System.Drawing.Point(3, 3);
            this.directorySplitContainer.Name = "directorySplitContainer";
            // 
            // directorySplitContainer.Panel1
            // 
            this.directorySplitContainer.Panel1.Controls.Add(this.directoryTreeView);
            // 
            // directorySplitContainer.Panel2
            // 
            this.directorySplitContainer.Panel2.Controls.Add(this.directoryListView);
            this.directorySplitContainer.Size = new System.Drawing.Size(538, 393);
            this.directorySplitContainer.SplitterDistance = 179;
            this.directorySplitContainer.TabIndex = 0;
            // 
            // directoryBottomPanel
            // 
            this.directoryBottomPanel.Controls.Add(this.copyPathButton);
            this.directoryBottomPanel.Controls.Add(this.directoryPathTextBox);
            this.directoryBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.directoryBottomPanel.Location = new System.Drawing.Point(3, 396);
            this.directoryBottomPanel.Name = "directoryBottomPanel";
            this.directoryBottomPanel.Size = new System.Drawing.Size(538, 27);
            this.directoryBottomPanel.TabIndex = 1;
            // 
            // copyPathButton
            // 
            this.copyPathButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.copyPathButton.Location = new System.Drawing.Point(3, 3);
            this.copyPathButton.Name = "copyPathButton";
            this.copyPathButton.Size = new System.Drawing.Size(75, 23);
            this.copyPathButton.TabIndex = 2;
            this.copyPathButton.Text = "Copy Path";
            this.copyPathButton.UseVisualStyleBackColor = true;
            // 
            // directoryPathTextBox
            // 
            this.directoryPathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.directoryPathTextBox.Location = new System.Drawing.Point(84, 5);
            this.directoryPathTextBox.Name = "directoryPathTextBox";
            this.directoryPathTextBox.Size = new System.Drawing.Size(449, 20);
            this.directoryPathTextBox.TabIndex = 1;
            // 
            // mainStatusStrip
            // 
            this.mainStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.catalogsLoadedStatus,
            this.searchResultsStatus,
            this.searchTimeStatus,
            this.toolStripStatusLabelRightAlign,
            this.toolStripDropDownButton1});
            this.mainStatusStrip.Location = new System.Drawing.Point(0, 476);
            this.mainStatusStrip.Name = "mainStatusStrip";
            this.mainStatusStrip.ShowItemToolTips = true;
            this.mainStatusStrip.Size = new System.Drawing.Size(552, 24);
            this.mainStatusStrip.TabIndex = 2;
            this.mainStatusStrip.Text = "mainStatusStrip";
            // 
            // catalogsLoadedStatus
            // 
            this.catalogsLoadedStatus.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.catalogsLoadedStatus.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.catalogsLoadedStatus.Name = "catalogsLoadedStatus";
            this.catalogsLoadedStatus.Size = new System.Drawing.Size(28, 19);
            this.catalogsLoadedStatus.Text = "C 0";
            this.catalogsLoadedStatus.ToolTipText = "Catalogs Loaded";
            // 
            // searchResultsStatus
            // 
            this.searchResultsStatus.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.searchResultsStatus.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.searchResultsStatus.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.searchResultsStatus.Name = "searchResultsStatus";
            this.searchResultsStatus.Size = new System.Drawing.Size(33, 19);
            this.searchResultsStatus.Text = "SR 0";
            this.searchResultsStatus.ToolTipText = "Search Results Found";
            // 
            // toolStripStatusLabelRightAlign
            // 
            this.toolStripStatusLabelRightAlign.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusLabelRightAlign.Name = "toolStripStatusLabelRightAlign";
            this.toolStripStatusLabelRightAlign.Size = new System.Drawing.Size(351, 19);
            this.toolStripStatusLabelRightAlign.Spring = true;
            // 
            // toolStripDropDownButton1
            // 
            this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButton1.Image")));
            this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            this.toolStripDropDownButton1.Size = new System.Drawing.Size(81, 22);
            this.toolStripDropDownButton1.Text = "Test Button";
            // 
            // searchTimeStatus
            // 
            this.searchTimeStatus.Name = "searchTimeStatus";
            this.searchTimeStatus.Size = new System.Drawing.Size(12, 19);
            this.searchTimeStatus.Text = "*";
            // 
            // CDEWinForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(552, 500);
            this.Controls.Add(this.mainTabControl);
            this.Controls.Add(this.mainStatusStrip);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.MinimumSize = new System.Drawing.Size(400, 300);
            this.Name = "CDEWinForm";
            this.Text = "cdeWinView";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.mainTabControl.ResumeLayout(false);
            this.searchTab.ResumeLayout(false);
            this.searchResultPanel.ResumeLayout(false);
            this.searchControlPanel.ResumeLayout(false);
            this.searchControlPanel.PerformLayout();
            this.catalogTab.ResumeLayout(false);
            this.catalogResultPanel.ResumeLayout(false);
            this.catalogControlPanel.ResumeLayout(false);
            this.catalogControlPanel.PerformLayout();
            this.directoryTab.ResumeLayout(false);
            this.directorySplitContainer.Panel1.ResumeLayout(false);
            this.directorySplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.directorySplitContainer)).EndInit();
            this.directorySplitContainer.ResumeLayout(false);
            this.directoryBottomPanel.ResumeLayout(false);
            this.directoryBottomPanel.PerformLayout();
            this.mainStatusStrip.ResumeLayout(false);
            this.mainStatusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TreeView directoryTreeView;
        private System.Windows.Forms.ListView directoryListView;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.TabControl mainTabControl;
        private System.Windows.Forms.TabPage searchTab;
        private System.Windows.Forms.TabPage catalogTab;
        private System.Windows.Forms.TabPage directoryTab;
        private System.Windows.Forms.SplitContainer directorySplitContainer;
        private System.Windows.Forms.Panel searchResultPanel;
        private System.Windows.Forms.Panel searchControlPanel;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.Panel catalogResultPanel;
        private System.Windows.Forms.Panel catalogControlPanel;
        private System.Windows.Forms.CheckBox regexCheckbox;
        private System.Windows.Forms.ListView searchResultListView;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.ListView catalogResultListView;
        private System.Windows.Forms.StatusStrip mainStatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel searchResultsStatus;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelRightAlign;
        private System.Windows.Forms.ToolStripStatusLabel catalogsLoadedStatus;
        private System.Windows.Forms.CheckBox checkboxFolders;
        private System.Windows.Forms.CheckBox checkboxFiles;
        private System.Windows.Forms.ComboBox whatToSearchComboBox;
        private System.Windows.Forms.Label labelSearchPath;
        private System.Windows.Forms.TextBox patternTextBox;
        private System.Windows.Forms.Label labelCatalogPlaceholder;
        private System.Windows.Forms.Panel directoryBottomPanel;
        private System.Windows.Forms.Button copyPathButton;
        private System.Windows.Forms.TextBox directoryPathTextBox;
        private System.Windows.Forms.ToolStripStatusLabel searchTimeStatus;
    }
}


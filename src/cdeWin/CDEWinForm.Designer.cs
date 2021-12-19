using cdeWin.Properties;

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
            this.directoryTreeView = new System.Windows.Forms.TreeView();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mainTabControl = new System.Windows.Forms.TabControl();
            this.searchTab = new System.Windows.Forms.TabPage();
            this.searchResultPanel = new System.Windows.Forms.Panel();
            this.searchResultListView = new cdeWin.DoubleBufferListView();
            this.searchControlPanel = new System.Windows.Forms.TableLayoutPanel();
            this.searchControlUpperPanel = new System.Windows.Forms.Panel();
            this.advancedSearchCheckBox = new System.Windows.Forms.CheckBox();
            this.limitResultDropDown = new System.Windows.Forms.ComboBox();
            this.searchButton = new System.Windows.Forms.Button();
            this.findLabel = new System.Windows.Forms.Label();
            this.findComboBox = new System.Windows.Forms.ComboBox();
            this.patternComboBox = new System.Windows.Forms.ComboBox();
            this.whatToSearchComboBox = new System.Windows.Forms.ComboBox();
            this.regexCheckbox = new System.Windows.Forms.CheckBox();
            this.searchLabel = new System.Windows.Forms.Label();
            this.searchControlAdvancedPanel = new System.Windows.Forms.Panel();
            this.fromDateCheckbox = new System.Windows.Forms.CheckBox();
            this.fromDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.toDateCheckbox = new System.Windows.Forms.CheckBox();
            this.toDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.fromHourCheckbox = new System.Windows.Forms.CheckBox();
            this.fromHourTimePicker = new System.Windows.Forms.DateTimePicker();
            this.toHourCheckbox = new System.Windows.Forms.CheckBox();
            this.toHourTimePicker = new System.Windows.Forms.DateTimePicker();
            this.fromSizeCheckbox = new System.Windows.Forms.CheckBox();
            this.fromSizeUpDown = new System.Windows.Forms.NumericUpDown();
            this.fromSizeDropDown = new System.Windows.Forms.ComboBox();
            this.toSizeCheckbox = new System.Windows.Forms.CheckBox();
            this.toSizeUpDown = new System.Windows.Forms.NumericUpDown();
            this.toSizeDropDown = new System.Windows.Forms.ComboBox();
            this.notOlderThanCheckbox = new System.Windows.Forms.CheckBox();
            this.notOlderThanUpDown = new System.Windows.Forms.NumericUpDown();
            this.notOlderThanDropDown = new System.Windows.Forms.ComboBox();
            this.catalogTab = new System.Windows.Forms.TabPage();
            this.catalogResultPanel = new System.Windows.Forms.Panel();
            this.catalogResultListView = new cdeWin.DoubleBufferListView();
            this.catalogControlPanel = new System.Windows.Forms.Panel();
            this.reloadCatalogsButton = new System.Windows.Forms.Button();
            this.labelCatalogPlaceholder = new System.Windows.Forms.Label();
            this.directoryTab = new System.Windows.Forms.TabPage();
            this.directorySplitContainer = new System.Windows.Forms.SplitContainer();
            this.directoryListView = new cdeWin.DoubleBufferListView();
            this.directoryBottomPanel = new System.Windows.Forms.Panel();
            this.copyPathButton = new System.Windows.Forms.Button();
            this.directoryPathTextBox = new System.Windows.Forms.TextBox();
            this.logTab = new System.Windows.Forms.TabPage();
            this.tbLog = new System.Windows.Forms.TextBox();
            this.bottomLogPanel = new System.Windows.Forms.Panel();
            this.bottomLogButton = new System.Windows.Forms.Button();
            this.mainStatusStrip = new System.Windows.Forms.StatusStrip();
            this.catalogsLoadedStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.totalFileEntriesStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusMemory = new System.Windows.Forms.ToolStripStatusLabel();
            this.searchResultsStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.searchTimeStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelRightAlign = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1.SuspendLayout();
            this.mainTabControl.SuspendLayout();
            this.searchTab.SuspendLayout();
            this.searchResultPanel.SuspendLayout();
            this.searchControlPanel.SuspendLayout();
            this.searchControlUpperPanel.SuspendLayout();
            this.searchControlAdvancedPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fromSizeUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.toSizeUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.notOlderThanUpDown)).BeginInit();
            this.catalogTab.SuspendLayout();
            this.catalogResultPanel.SuspendLayout();
            this.catalogControlPanel.SuspendLayout();
            this.directoryTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.directorySplitContainer)).BeginInit();
            this.directorySplitContainer.Panel1.SuspendLayout();
            this.directorySplitContainer.Panel2.SuspendLayout();
            this.directorySplitContainer.SuspendLayout();
            this.directoryBottomPanel.SuspendLayout();
            this.logTab.SuspendLayout();
            this.bottomLogPanel.SuspendLayout();
            this.mainStatusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // directoryTreeView
            // 
            this.directoryTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directoryTreeView.HideSelection = false;
            this.directoryTreeView.Location = new System.Drawing.Point(0, 0);
            this.directoryTreeView.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.directoryTreeView.Name = "directoryTreeView";
            this.directoryTreeView.Size = new System.Drawing.Size(296, 627);
            this.directoryTreeView.TabIndex = 0;
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(8, 3, 0, 3);
            this.menuStrip1.Size = new System.Drawing.Size(912, 30);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(46, 24);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(133, 26);
            this.aboutToolStripMenuItem.Text = "About";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(133, 26);
            this.exitToolStripMenuItem.Text = "Exit";
            // 
            // mainTabControl
            // 
            this.mainTabControl.Controls.Add(this.searchTab);
            this.mainTabControl.Controls.Add(this.catalogTab);
            this.mainTabControl.Controls.Add(this.directoryTab);
            this.mainTabControl.Controls.Add(this.logTab);
            this.mainTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTabControl.Location = new System.Drawing.Point(0, 30);
            this.mainTabControl.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.mainTabControl.Name = "mainTabControl";
            this.mainTabControl.SelectedIndex = 0;
            this.mainTabControl.Size = new System.Drawing.Size(912, 709);
            this.mainTabControl.TabIndex = 0;
            // 
            // searchTab
            // 
            this.searchTab.Controls.Add(this.searchResultPanel);
            this.searchTab.Controls.Add(this.searchControlPanel);
            this.searchTab.Location = new System.Drawing.Point(4, 29);
            this.searchTab.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchTab.Name = "searchTab";
            this.searchTab.Padding = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchTab.Size = new System.Drawing.Size(904, 676);
            this.searchTab.TabIndex = 0;
            this.searchTab.Text = "Search";
            this.searchTab.UseVisualStyleBackColor = true;
            // 
            // searchResultPanel
            // 
            this.searchResultPanel.Controls.Add(this.searchResultListView);
            this.searchResultPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchResultPanel.Location = new System.Drawing.Point(5, 222);
            this.searchResultPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchResultPanel.Name = "searchResultPanel";
            this.searchResultPanel.Size = new System.Drawing.Size(894, 450);
            this.searchResultPanel.TabIndex = 1;
            // 
            // searchResultListView
            // 
            this.searchResultListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchResultListView.Location = new System.Drawing.Point(0, 0);
            this.searchResultListView.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchResultListView.Name = "searchResultListView";
            this.searchResultListView.Size = new System.Drawing.Size(894, 450);
            this.searchResultListView.TabIndex = 0;
            this.searchResultListView.UseCompatibleStateImageBehavior = false;
            // 
            // searchControlPanel
            // 
            this.searchControlPanel.AutoSize = true;
            this.searchControlPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.searchControlPanel.Controls.Add(this.searchControlUpperPanel, 0, 0);
            this.searchControlPanel.Controls.Add(this.searchControlAdvancedPanel, 0, 1);
            this.searchControlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchControlPanel.Location = new System.Drawing.Point(5, 4);
            this.searchControlPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchControlPanel.Name = "searchControlPanel";
            this.searchControlPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.searchControlPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.searchControlPanel.Size = new System.Drawing.Size(894, 218);
            this.searchControlPanel.TabIndex = 26;
            // 
            // searchControlUpperPanel
            // 
            this.searchControlUpperPanel.Controls.Add(this.advancedSearchCheckBox);
            this.searchControlUpperPanel.Controls.Add(this.limitResultDropDown);
            this.searchControlUpperPanel.Controls.Add(this.searchButton);
            this.searchControlUpperPanel.Controls.Add(this.findLabel);
            this.searchControlUpperPanel.Controls.Add(this.findComboBox);
            this.searchControlUpperPanel.Controls.Add(this.patternComboBox);
            this.searchControlUpperPanel.Controls.Add(this.whatToSearchComboBox);
            this.searchControlUpperPanel.Controls.Add(this.regexCheckbox);
            this.searchControlUpperPanel.Controls.Add(this.searchLabel);
            this.searchControlUpperPanel.Location = new System.Drawing.Point(5, 4);
            this.searchControlUpperPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchControlUpperPanel.Name = "searchControlUpperPanel";
            this.searchControlUpperPanel.Size = new System.Drawing.Size(884, 77);
            this.searchControlUpperPanel.TabIndex = 0;
            // 
            // advancedSearchCheckBox
            // 
            this.advancedSearchCheckBox.AutoSize = true;
            this.advancedSearchCheckBox.Location = new System.Drawing.Point(462, 41);
            this.advancedSearchCheckBox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.advancedSearchCheckBox.Name = "advancedSearchCheckBox";
            this.advancedSearchCheckBox.Size = new System.Drawing.Size(145, 24);
            this.advancedSearchCheckBox.TabIndex = 26;
            this.advancedSearchCheckBox.Text = "&Advanced Search";
            this.advancedSearchCheckBox.UseVisualStyleBackColor = true;
            // 
            // limitResultDropDown
            // 
            this.limitResultDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.limitResultDropDown.FormattingEnabled = true;
            this.limitResultDropDown.Location = new System.Drawing.Point(618, 39);
            this.limitResultDropDown.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.limitResultDropDown.Name = "limitResultDropDown";
            this.limitResultDropDown.Size = new System.Drawing.Size(164, 28);
            this.limitResultDropDown.TabIndex = 25;
            // 
            // searchButton
            // 
            this.searchButton.Location = new System.Drawing.Point(618, 0);
            this.searchButton.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(128, 36);
            this.searchButton.TabIndex = 3;
            this.searchButton.Text = "&Search";
            this.searchButton.UseVisualStyleBackColor = true;
            // 
            // findLabel
            // 
            this.findLabel.AutoSize = true;
            this.findLabel.Location = new System.Drawing.Point(59, 43);
            this.findLabel.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.findLabel.Name = "findLabel";
            this.findLabel.Size = new System.Drawing.Size(37, 20);
            this.findLabel.TabIndex = 4;
            this.findLabel.Text = "Find";
            // 
            // findComboBox
            // 
            this.findComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.findComboBox.FormattingEnabled = true;
            this.findComboBox.Location = new System.Drawing.Point(104, 39);
            this.findComboBox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.findComboBox.Name = "findComboBox";
            this.findComboBox.Size = new System.Drawing.Size(147, 28);
            this.findComboBox.TabIndex = 5;
            // 
            // patternComboBox
            // 
            this.patternComboBox.Location = new System.Drawing.Point(104, 3);
            this.patternComboBox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.patternComboBox.Name = "patternComboBox";
            this.patternComboBox.Size = new System.Drawing.Size(348, 28);
            this.patternComboBox.TabIndex = 1;
            // 
            // whatToSearchComboBox
            // 
            this.whatToSearchComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.whatToSearchComboBox.FormattingEnabled = true;
            this.whatToSearchComboBox.Location = new System.Drawing.Point(259, 39);
            this.whatToSearchComboBox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.whatToSearchComboBox.Name = "whatToSearchComboBox";
            this.whatToSearchComboBox.Size = new System.Drawing.Size(191, 28);
            this.whatToSearchComboBox.TabIndex = 6;
            // 
            // regexCheckbox
            // 
            this.regexCheckbox.AutoSize = true;
            this.regexCheckbox.Location = new System.Drawing.Point(462, 7);
            this.regexCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.regexCheckbox.Name = "regexCheckbox";
            this.regexCheckbox.Size = new System.Drawing.Size(72, 24);
            this.regexCheckbox.TabIndex = 2;
            this.regexCheckbox.Text = "Rege&x";
            this.regexCheckbox.UseVisualStyleBackColor = true;
            // 
            // searchLabel
            // 
            this.searchLabel.AutoSize = true;
            this.searchLabel.Location = new System.Drawing.Point(41, 7);
            this.searchLabel.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.searchLabel.Name = "searchLabel";
            this.searchLabel.Size = new System.Drawing.Size(55, 20);
            this.searchLabel.TabIndex = 0;
            this.searchLabel.Text = "&Pattern";
            // 
            // searchControlAdvancedPanel
            // 
            this.searchControlAdvancedPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.searchControlAdvancedPanel.Controls.Add(this.fromDateCheckbox);
            this.searchControlAdvancedPanel.Controls.Add(this.fromDateTimePicker);
            this.searchControlAdvancedPanel.Controls.Add(this.toDateCheckbox);
            this.searchControlAdvancedPanel.Controls.Add(this.toDateTimePicker);
            this.searchControlAdvancedPanel.Controls.Add(this.fromHourCheckbox);
            this.searchControlAdvancedPanel.Controls.Add(this.fromHourTimePicker);
            this.searchControlAdvancedPanel.Controls.Add(this.toHourCheckbox);
            this.searchControlAdvancedPanel.Controls.Add(this.toHourTimePicker);
            this.searchControlAdvancedPanel.Controls.Add(this.fromSizeCheckbox);
            this.searchControlAdvancedPanel.Controls.Add(this.fromSizeUpDown);
            this.searchControlAdvancedPanel.Controls.Add(this.fromSizeDropDown);
            this.searchControlAdvancedPanel.Controls.Add(this.toSizeCheckbox);
            this.searchControlAdvancedPanel.Controls.Add(this.toSizeUpDown);
            this.searchControlAdvancedPanel.Controls.Add(this.toSizeDropDown);
            this.searchControlAdvancedPanel.Controls.Add(this.notOlderThanCheckbox);
            this.searchControlAdvancedPanel.Controls.Add(this.notOlderThanUpDown);
            this.searchControlAdvancedPanel.Controls.Add(this.notOlderThanDropDown);
            this.searchControlAdvancedPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchControlAdvancedPanel.Location = new System.Drawing.Point(5, 89);
            this.searchControlAdvancedPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.searchControlAdvancedPanel.Name = "searchControlAdvancedPanel";
            this.searchControlAdvancedPanel.Size = new System.Drawing.Size(884, 125);
            this.searchControlAdvancedPanel.TabIndex = 25;
            // 
            // fromDateCheckbox
            // 
            this.fromDateCheckbox.AutoSize = true;
            this.fromDateCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.fromDateCheckbox.Location = new System.Drawing.Point(39, 12);
            this.fromDateCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.fromDateCheckbox.Name = "fromDateCheckbox";
            this.fromDateCheckbox.Size = new System.Drawing.Size(101, 24);
            this.fromDateCheckbox.TabIndex = 8;
            this.fromDateCheckbox.Text = "From Date";
            this.fromDateCheckbox.UseVisualStyleBackColor = true;
            // 
            // fromDateTimePicker
            // 
            this.fromDateTimePicker.CustomFormat = "yyyy/MM/dd";
            this.fromDateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.fromDateTimePicker.Location = new System.Drawing.Point(146, 9);
            this.fromDateTimePicker.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.fromDateTimePicker.Name = "fromDateTimePicker";
            this.fromDateTimePicker.Size = new System.Drawing.Size(135, 27);
            this.fromDateTimePicker.TabIndex = 9;
            // 
            // toDateCheckbox
            // 
            this.toDateCheckbox.AutoSize = true;
            this.toDateCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toDateCheckbox.Location = new System.Drawing.Point(56, 52);
            this.toDateCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.toDateCheckbox.Name = "toDateCheckbox";
            this.toDateCheckbox.Size = new System.Drawing.Size(83, 24);
            this.toDateCheckbox.TabIndex = 10;
            this.toDateCheckbox.Text = "To Date";
            this.toDateCheckbox.UseVisualStyleBackColor = true;
            // 
            // toDateTimePicker
            // 
            this.toDateTimePicker.CustomFormat = "yyyy/MM/dd";
            this.toDateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.toDateTimePicker.Location = new System.Drawing.Point(146, 49);
            this.toDateTimePicker.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.toDateTimePicker.Name = "toDateTimePicker";
            this.toDateTimePicker.Size = new System.Drawing.Size(135, 27);
            this.toDateTimePicker.TabIndex = 11;
            // 
            // fromHourCheckbox
            // 
            this.fromHourCheckbox.AutoSize = true;
            this.fromHourCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.fromHourCheckbox.Location = new System.Drawing.Point(291, 13);
            this.fromHourCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.fromHourCheckbox.Name = "fromHourCheckbox";
            this.fromHourCheckbox.Size = new System.Drawing.Size(102, 24);
            this.fromHourCheckbox.TabIndex = 12;
            this.fromHourCheckbox.Text = "From Hour";
            this.fromHourCheckbox.UseVisualStyleBackColor = true;
            // 
            // fromHourTimePicker
            // 
            this.fromHourTimePicker.CustomFormat = "HH:mm:ss";
            this.fromHourTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.fromHourTimePicker.Location = new System.Drawing.Point(400, 9);
            this.fromHourTimePicker.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.fromHourTimePicker.Name = "fromHourTimePicker";
            this.fromHourTimePicker.ShowUpDown = true;
            this.fromHourTimePicker.Size = new System.Drawing.Size(116, 27);
            this.fromHourTimePicker.TabIndex = 13;
            // 
            // toHourCheckbox
            // 
            this.toHourCheckbox.AutoSize = true;
            this.toHourCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toHourCheckbox.Location = new System.Drawing.Point(308, 52);
            this.toHourCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.toHourCheckbox.Name = "toHourCheckbox";
            this.toHourCheckbox.Size = new System.Drawing.Size(84, 24);
            this.toHourCheckbox.TabIndex = 14;
            this.toHourCheckbox.Text = "To Hour";
            this.toHourCheckbox.UseVisualStyleBackColor = true;
            // 
            // toHourTimePicker
            // 
            this.toHourTimePicker.CustomFormat = "HH:mm:ss";
            this.toHourTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.toHourTimePicker.Location = new System.Drawing.Point(400, 49);
            this.toHourTimePicker.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.toHourTimePicker.Name = "toHourTimePicker";
            this.toHourTimePicker.ShowUpDown = true;
            this.toHourTimePicker.Size = new System.Drawing.Size(116, 27);
            this.toHourTimePicker.TabIndex = 15;
            // 
            // fromSizeCheckbox
            // 
            this.fromSizeCheckbox.AutoSize = true;
            this.fromSizeCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.fromSizeCheckbox.Location = new System.Drawing.Point(551, 12);
            this.fromSizeCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.fromSizeCheckbox.Name = "fromSizeCheckbox";
            this.fromSizeCheckbox.Size = new System.Drawing.Size(96, 24);
            this.fromSizeCheckbox.TabIndex = 16;
            this.fromSizeCheckbox.Text = "From Size";
            this.fromSizeCheckbox.UseVisualStyleBackColor = true;
            // 
            // fromSizeUpDown
            // 
            this.fromSizeUpDown.Location = new System.Drawing.Point(655, 9);
            this.fromSizeUpDown.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.fromSizeUpDown.Name = "fromSizeUpDown";
            this.fromSizeUpDown.Size = new System.Drawing.Size(91, 27);
            this.fromSizeUpDown.TabIndex = 17;
            // 
            // fromSizeDropDown
            // 
            this.fromSizeDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.fromSizeDropDown.FormattingEnabled = true;
            this.fromSizeDropDown.Location = new System.Drawing.Point(754, 9);
            this.fromSizeDropDown.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.fromSizeDropDown.Name = "fromSizeDropDown";
            this.fromSizeDropDown.Size = new System.Drawing.Size(99, 28);
            this.fromSizeDropDown.TabIndex = 18;
            // 
            // toSizeCheckbox
            // 
            this.toSizeCheckbox.AutoSize = true;
            this.toSizeCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toSizeCheckbox.Location = new System.Drawing.Point(568, 51);
            this.toSizeCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.toSizeCheckbox.Name = "toSizeCheckbox";
            this.toSizeCheckbox.Size = new System.Drawing.Size(78, 24);
            this.toSizeCheckbox.TabIndex = 19;
            this.toSizeCheckbox.Text = "To Size";
            this.toSizeCheckbox.UseVisualStyleBackColor = true;
            // 
            // toSizeUpDown
            // 
            this.toSizeUpDown.Location = new System.Drawing.Point(655, 49);
            this.toSizeUpDown.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.toSizeUpDown.Name = "toSizeUpDown";
            this.toSizeUpDown.Size = new System.Drawing.Size(91, 27);
            this.toSizeUpDown.TabIndex = 20;
            // 
            // toSizeDropDown
            // 
            this.toSizeDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.toSizeDropDown.FormattingEnabled = true;
            this.toSizeDropDown.Location = new System.Drawing.Point(754, 49);
            this.toSizeDropDown.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.toSizeDropDown.Name = "toSizeDropDown";
            this.toSizeDropDown.Size = new System.Drawing.Size(99, 28);
            this.toSizeDropDown.TabIndex = 21;
            // 
            // notOlderThanCheckbox
            // 
            this.notOlderThanCheckbox.AutoSize = true;
            this.notOlderThanCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.notOlderThanCheckbox.Location = new System.Drawing.Point(6, 86);
            this.notOlderThanCheckbox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.notOlderThanCheckbox.Name = "notOlderThanCheckbox";
            this.notOlderThanCheckbox.Size = new System.Drawing.Size(133, 24);
            this.notOlderThanCheckbox.TabIndex = 22;
            this.notOlderThanCheckbox.Text = "Not Older Than";
            this.notOlderThanCheckbox.UseVisualStyleBackColor = true;
            // 
            // notOlderThanUpDown
            // 
            this.notOlderThanUpDown.Location = new System.Drawing.Point(146, 84);
            this.notOlderThanUpDown.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.notOlderThanUpDown.Name = "notOlderThanUpDown";
            this.notOlderThanUpDown.Size = new System.Drawing.Size(105, 27);
            this.notOlderThanUpDown.TabIndex = 23;
            // 
            // notOlderThanDropDown
            // 
            this.notOlderThanDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.notOlderThanDropDown.FormattingEnabled = true;
            this.notOlderThanDropDown.Location = new System.Drawing.Point(259, 83);
            this.notOlderThanDropDown.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.notOlderThanDropDown.Name = "notOlderThanDropDown";
            this.notOlderThanDropDown.Size = new System.Drawing.Size(99, 28);
            this.notOlderThanDropDown.TabIndex = 24;
            // 
            // catalogTab
            // 
            this.catalogTab.Controls.Add(this.catalogResultPanel);
            this.catalogTab.Controls.Add(this.catalogControlPanel);
            this.catalogTab.Location = new System.Drawing.Point(4, 29);
            this.catalogTab.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.catalogTab.Name = "catalogTab";
            this.catalogTab.Padding = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.catalogTab.Size = new System.Drawing.Size(904, 676);
            this.catalogTab.TabIndex = 1;
            this.catalogTab.Text = "Catalog";
            this.catalogTab.UseVisualStyleBackColor = true;
            // 
            // catalogResultPanel
            // 
            this.catalogResultPanel.Controls.Add(this.catalogResultListView);
            this.catalogResultPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.catalogResultPanel.Location = new System.Drawing.Point(5, 128);
            this.catalogResultPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.catalogResultPanel.Name = "catalogResultPanel";
            this.catalogResultPanel.Size = new System.Drawing.Size(894, 544);
            this.catalogResultPanel.TabIndex = 1;
            // 
            // catalogResultListView
            // 
            this.catalogResultListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.catalogResultListView.Location = new System.Drawing.Point(0, 0);
            this.catalogResultListView.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.catalogResultListView.Name = "catalogResultListView";
            this.catalogResultListView.Size = new System.Drawing.Size(894, 544);
            this.catalogResultListView.TabIndex = 0;
            this.catalogResultListView.UseCompatibleStateImageBehavior = false;
            // 
            // catalogControlPanel
            // 
            this.catalogControlPanel.Controls.Add(this.reloadCatalogsButton);
            this.catalogControlPanel.Controls.Add(this.labelCatalogPlaceholder);
            this.catalogControlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.catalogControlPanel.Location = new System.Drawing.Point(5, 4);
            this.catalogControlPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.catalogControlPanel.Name = "catalogControlPanel";
            this.catalogControlPanel.Size = new System.Drawing.Size(894, 124);
            this.catalogControlPanel.TabIndex = 0;
            // 
            // reloadCatalogsButton
            // 
            this.reloadCatalogsButton.Location = new System.Drawing.Point(6, 7);
            this.reloadCatalogsButton.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.reloadCatalogsButton.Name = "reloadCatalogsButton";
            this.reloadCatalogsButton.Size = new System.Drawing.Size(177, 36);
            this.reloadCatalogsButton.TabIndex = 1;
            this.reloadCatalogsButton.Text = "Reload Catalogs";
            this.reloadCatalogsButton.UseVisualStyleBackColor = true;
            // 
            // labelCatalogPlaceholder
            // 
            this.labelCatalogPlaceholder.AutoSize = true;
            this.labelCatalogPlaceholder.Location = new System.Drawing.Point(406, 100);
            this.labelCatalogPlaceholder.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.labelCatalogPlaceholder.Name = "labelCatalogPlaceholder";
            this.labelCatalogPlaceholder.Size = new System.Drawing.Size(318, 20);
            this.labelCatalogPlaceholder.TabIndex = 0;
            this.labelCatalogPlaceholder.Text = "Place to put create catalog controls eventually.";
            // 
            // directoryTab
            // 
            this.directoryTab.Controls.Add(this.directorySplitContainer);
            this.directoryTab.Controls.Add(this.directoryBottomPanel);
            this.directoryTab.Location = new System.Drawing.Point(4, 29);
            this.directoryTab.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.directoryTab.Name = "directoryTab";
            this.directoryTab.Padding = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.directoryTab.Size = new System.Drawing.Size(904, 676);
            this.directoryTab.TabIndex = 2;
            this.directoryTab.Text = "Directory";
            this.directoryTab.UseVisualStyleBackColor = true;
            // 
            // directorySplitContainer
            // 
            this.directorySplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directorySplitContainer.Location = new System.Drawing.Point(5, 4);
            this.directorySplitContainer.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.directorySplitContainer.Name = "directorySplitContainer";
            // 
            // directorySplitContainer.Panel1
            // 
            this.directorySplitContainer.Panel1.Controls.Add(this.directoryTreeView);
            // 
            // directorySplitContainer.Panel2
            // 
            this.directorySplitContainer.Panel2.Controls.Add(this.directoryListView);
            this.directorySplitContainer.Size = new System.Drawing.Size(894, 627);
            this.directorySplitContainer.SplitterDistance = 296;
            this.directorySplitContainer.SplitterWidth = 6;
            this.directorySplitContainer.TabIndex = 0;
            // 
            // directoryListView
            // 
            this.directoryListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directoryListView.Location = new System.Drawing.Point(0, 0);
            this.directoryListView.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.directoryListView.Name = "directoryListView";
            this.directoryListView.Size = new System.Drawing.Size(592, 627);
            this.directoryListView.TabIndex = 2;
            this.directoryListView.UseCompatibleStateImageBehavior = false;
            // 
            // directoryBottomPanel
            // 
            this.directoryBottomPanel.Controls.Add(this.copyPathButton);
            this.directoryBottomPanel.Controls.Add(this.directoryPathTextBox);
            this.directoryBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.directoryBottomPanel.Location = new System.Drawing.Point(5, 631);
            this.directoryBottomPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.directoryBottomPanel.Name = "directoryBottomPanel";
            this.directoryBottomPanel.Size = new System.Drawing.Size(894, 41);
            this.directoryBottomPanel.TabIndex = 1;
            // 
            // copyPathButton
            // 
            this.copyPathButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.copyPathButton.Location = new System.Drawing.Point(5, 4);
            this.copyPathButton.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.copyPathButton.Name = "copyPathButton";
            this.copyPathButton.Size = new System.Drawing.Size(101, 36);
            this.copyPathButton.TabIndex = 2;
            this.copyPathButton.Text = "nothing";
            this.copyPathButton.UseVisualStyleBackColor = true;
            this.copyPathButton.Visible = false;
            // 
            // directoryPathTextBox
            // 
            this.directoryPathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.directoryPathTextBox.Location = new System.Drawing.Point(112, -84);
            this.directoryPathTextBox.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.directoryPathTextBox.Name = "directoryPathTextBox";
            this.directoryPathTextBox.Size = new System.Drawing.Size(773, 27);
            this.directoryPathTextBox.TabIndex = 1;
            // 
            // logTab
            // 
            this.logTab.Controls.Add(this.tbLog);
            this.logTab.Controls.Add(this.bottomLogPanel);
            this.logTab.Location = new System.Drawing.Point(4, 29);
            this.logTab.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.logTab.Name = "logTab";
            this.logTab.Size = new System.Drawing.Size(904, 672);
            this.logTab.TabIndex = 3;
            this.logTab.Text = "Log";
            this.logTab.UseVisualStyleBackColor = true;
            // 
            // tbLog
            // 
            this.tbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbLog.Location = new System.Drawing.Point(0, 0);
            this.tbLog.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.tbLog.Multiline = true;
            this.tbLog.Name = "tbLog";
            this.tbLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbLog.Size = new System.Drawing.Size(904, 628);
            this.tbLog.TabIndex = 0;
            // 
            // bottomLogPanel
            // 
            this.bottomLogPanel.Controls.Add(this.bottomLogButton);
            this.bottomLogPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomLogPanel.Location = new System.Drawing.Point(0, 628);
            this.bottomLogPanel.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.bottomLogPanel.Name = "bottomLogPanel";
            this.bottomLogPanel.Size = new System.Drawing.Size(904, 44);
            this.bottomLogPanel.TabIndex = 2;
            // 
            // bottomLogButton
            // 
            this.bottomLogButton.Location = new System.Drawing.Point(5, 4);
            this.bottomLogButton.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.bottomLogButton.Name = "bottomLogButton";
            this.bottomLogButton.Size = new System.Drawing.Size(101, 36);
            this.bottomLogButton.TabIndex = 1;
            this.bottomLogButton.Text = "nothing";
            this.bottomLogButton.UseVisualStyleBackColor = true;
            this.bottomLogButton.Visible = false;
            // 
            // mainStatusStrip
            // 
            this.mainStatusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.mainStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.catalogsLoadedStatus,
            this.totalFileEntriesStatus,
            this.toolStripStatusMemory,
            this.searchResultsStatus,
            this.searchTimeStatus,
            this.toolStripStatusLabelRightAlign});
            this.mainStatusStrip.Location = new System.Drawing.Point(0, 739);
            this.mainStatusStrip.Name = "mainStatusStrip";
            this.mainStatusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 18, 0);
            this.mainStatusStrip.ShowItemToolTips = true;
            this.mainStatusStrip.Size = new System.Drawing.Size(912, 30);
            this.mainStatusStrip.TabIndex = 2;
            this.mainStatusStrip.Text = "mainStatusStrip";
            this.mainStatusStrip.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.mainStatusStrip_ItemClicked);
            // 
            // catalogsLoadedStatus
            // 
            this.catalogsLoadedStatus.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.catalogsLoadedStatus.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.catalogsLoadedStatus.Name = "catalogsLoadedStatus";
            this.catalogsLoadedStatus.Size = new System.Drawing.Size(83, 24);
            this.catalogsLoadedStatus.Text = "Catalogs 0";
            this.catalogsLoadedStatus.ToolTipText = "Catalogs Loaded";
            // 
            // totalFileEntriesStatus
            // 
            this.totalFileEntriesStatus.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.totalFileEntriesStatus.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.totalFileEntriesStatus.Name = "totalFileEntriesStatus";
            this.totalFileEntriesStatus.Size = new System.Drawing.Size(69, 24);
            this.totalFileEntriesStatus.Text = "Entries 0";
            this.totalFileEntriesStatus.ToolTipText = "Total File Entries in loaded catalogs";
            // 
            // toolStripStatusMemory
            // 
            this.toolStripStatusMemory.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.toolStripStatusMemory.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.toolStripStatusMemory.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripStatusMemory.Name = "toolStripStatusMemory";
            this.toolStripStatusMemory.Size = new System.Drawing.Size(166, 24);
            this.toolStripStatusMemory.Text = "toolStripStatusMemory";
            // 
            // searchResultsStatus
            // 
            this.searchResultsStatus.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.searchResultsStatus.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.searchResultsStatus.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.searchResultsStatus.Name = "searchResultsStatus";
            this.searchResultsStatus.Size = new System.Drawing.Size(119, 24);
            this.searchResultsStatus.Text = "Search Results 0";
            this.searchResultsStatus.ToolTipText = "Search Results Found (upto maximum)";
            // 
            // searchTimeStatus
            // 
            this.searchTimeStatus.Name = "searchTimeStatus";
            this.searchTimeStatus.Size = new System.Drawing.Size(15, 24);
            this.searchTimeStatus.Text = "*";
            this.searchTimeStatus.ToolTipText = "Search Time Taken msecs.";
            // 
            // toolStripStatusLabelRightAlign
            // 
            this.toolStripStatusLabelRightAlign.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusLabelRightAlign.Name = "toolStripStatusLabelRightAlign";
            this.toolStripStatusLabelRightAlign.Size = new System.Drawing.Size(441, 24);
            this.toolStripStatusLabelRightAlign.Spring = true;
            // 
            // CDEWinForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(912, 769);
            this.Controls.Add(this.mainTabControl);
            this.Controls.Add(this.mainStatusStrip);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
            this.MinimumSize = new System.Drawing.Size(528, 438);
            this.Name = "CDEWinForm";
            this.Text = "CDE";
            this.Shown += new System.EventHandler(this.CDEWinFormShown);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.mainTabControl.ResumeLayout(false);
            this.searchTab.ResumeLayout(false);
            this.searchTab.PerformLayout();
            this.searchResultPanel.ResumeLayout(false);
            this.searchControlPanel.ResumeLayout(false);
            this.searchControlUpperPanel.ResumeLayout(false);
            this.searchControlUpperPanel.PerformLayout();
            this.searchControlAdvancedPanel.ResumeLayout(false);
            this.searchControlAdvancedPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fromSizeUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.toSizeUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.notOlderThanUpDown)).EndInit();
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
            this.logTab.ResumeLayout(false);
            this.logTab.PerformLayout();
            this.bottomLogPanel.ResumeLayout(false);
            this.mainStatusStrip.ResumeLayout(false);
            this.mainStatusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TreeView directoryTreeView;
        private DoubleBufferListView directoryListView;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.TabControl mainTabControl;
        private System.Windows.Forms.TabPage searchTab;
        private System.Windows.Forms.TabPage catalogTab;
        private System.Windows.Forms.TabPage directoryTab;
        private System.Windows.Forms.SplitContainer directorySplitContainer;
        private System.Windows.Forms.Panel searchResultPanel;
        private System.Windows.Forms.Panel searchControlUpperPanel;
        private System.Windows.Forms.Label searchLabel;
        private System.Windows.Forms.Panel catalogResultPanel;
        private System.Windows.Forms.Panel catalogControlPanel;
        private System.Windows.Forms.CheckBox regexCheckbox;
        private DoubleBufferListView searchResultListView;
        private DoubleBufferListView catalogResultListView;
        private System.Windows.Forms.StatusStrip mainStatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel searchResultsStatus;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelRightAlign;
        private System.Windows.Forms.ToolStripStatusLabel catalogsLoadedStatus;
        private System.Windows.Forms.ComboBox whatToSearchComboBox;
        private System.Windows.Forms.ComboBox patternComboBox;
        private System.Windows.Forms.Label labelCatalogPlaceholder;
        private System.Windows.Forms.Panel directoryBottomPanel;
        private System.Windows.Forms.Button copyPathButton;
        private System.Windows.Forms.TextBox directoryPathTextBox;
        private System.Windows.Forms.ToolStripStatusLabel searchTimeStatus;
        private System.Windows.Forms.ComboBox findComboBox;
        private System.Windows.Forms.Label findLabel;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.TabPage logTab;
        private System.Windows.Forms.TextBox tbLog;
        private System.Windows.Forms.Panel bottomLogPanel;
		private System.Windows.Forms.Button bottomLogButton;
        private System.Windows.Forms.DateTimePicker toDateTimePicker;
        private System.Windows.Forms.DateTimePicker fromDateTimePicker;
        private System.Windows.Forms.CheckBox toDateCheckbox;
        private System.Windows.Forms.CheckBox fromDateCheckbox;
        private System.Windows.Forms.CheckBox fromHourCheckbox;
        private System.Windows.Forms.CheckBox toHourCheckbox;
        private System.Windows.Forms.DateTimePicker fromHourTimePicker;
        private System.Windows.Forms.DateTimePicker toHourTimePicker;
        private System.Windows.Forms.ComboBox notOlderThanDropDown;
        private System.Windows.Forms.NumericUpDown notOlderThanUpDown;
        private System.Windows.Forms.CheckBox notOlderThanCheckbox;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.CheckBox fromSizeCheckbox;
        private System.Windows.Forms.CheckBox toSizeCheckbox;
        private System.Windows.Forms.ComboBox toSizeDropDown;
        private System.Windows.Forms.ComboBox fromSizeDropDown;
        private System.Windows.Forms.NumericUpDown toSizeUpDown;
        private System.Windows.Forms.NumericUpDown fromSizeUpDown;
        private System.Windows.Forms.Panel searchControlAdvancedPanel;
        private System.Windows.Forms.TableLayoutPanel searchControlPanel;
        private System.Windows.Forms.ComboBox limitResultDropDown;
        private System.Windows.Forms.CheckBox advancedSearchCheckBox;
        private System.Windows.Forms.ToolStripStatusLabel totalFileEntriesStatus;
        private System.Windows.Forms.Button reloadCatalogsButton;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusMemory;
    }
}


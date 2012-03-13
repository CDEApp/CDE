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
            this.labelCatalogPlaceholder = new System.Windows.Forms.Label();
            this.directoryTab = new System.Windows.Forms.TabPage();
            this.directorySplitContainer = new System.Windows.Forms.SplitContainer();
            this.directoryListView = new cdeWin.DoubleBufferListView();
            this.directoryBottomPanel = new System.Windows.Forms.Panel();
            this.copyPathButton = new System.Windows.Forms.Button();
            this.directoryPathTextBox = new System.Windows.Forms.TextBox();
            this.logTab = new System.Windows.Forms.TabPage();
            this.listView1 = new System.Windows.Forms.ListView();
            this.bottomLogPanel = new System.Windows.Forms.Panel();
            this.logBottomPanelLabel = new System.Windows.Forms.Label();
            this.bottomLogButton = new System.Windows.Forms.Button();
            this.mainStatusStrip = new System.Windows.Forms.StatusStrip();
            this.catalogsLoadedStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.totalFileEntriesStatus = new System.Windows.Forms.ToolStripStatusLabel();
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
            this.directoryTreeView.Name = "directoryTreeView";
            this.directoryTreeView.Size = new System.Drawing.Size(222, 393);
            this.directoryTreeView.TabIndex = 0;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(684, 24);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.aboutToolStripMenuItem.Text = "About";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            // 
            // mainTabControl
            // 
            this.mainTabControl.Controls.Add(this.searchTab);
            this.mainTabControl.Controls.Add(this.catalogTab);
            this.mainTabControl.Controls.Add(this.directoryTab);
            this.mainTabControl.Controls.Add(this.logTab);
            this.mainTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTabControl.Location = new System.Drawing.Point(0, 24);
            this.mainTabControl.Name = "mainTabControl";
            this.mainTabControl.SelectedIndex = 0;
            this.mainTabControl.Size = new System.Drawing.Size(684, 452);
            this.mainTabControl.TabIndex = 0;
            // 
            // searchTab
            // 
            this.searchTab.Controls.Add(this.searchResultPanel);
            this.searchTab.Controls.Add(this.searchControlPanel);
            this.searchTab.Location = new System.Drawing.Point(4, 22);
            this.searchTab.Name = "searchTab";
            this.searchTab.Padding = new System.Windows.Forms.Padding(3);
            this.searchTab.Size = new System.Drawing.Size(676, 426);
            this.searchTab.TabIndex = 0;
            this.searchTab.Text = "Search";
            this.searchTab.UseVisualStyleBackColor = true;
            // 
            // searchResultPanel
            // 
            this.searchResultPanel.Controls.Add(this.searchResultListView);
            this.searchResultPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchResultPanel.Location = new System.Drawing.Point(3, 147);
            this.searchResultPanel.Name = "searchResultPanel";
            this.searchResultPanel.Size = new System.Drawing.Size(670, 276);
            this.searchResultPanel.TabIndex = 1;
            // 
            // searchResultListView
            // 
            this.searchResultListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.searchResultListView.Location = new System.Drawing.Point(0, 0);
            this.searchResultListView.Name = "searchResultListView";
            this.searchResultListView.Size = new System.Drawing.Size(670, 276);
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
            this.searchControlPanel.Location = new System.Drawing.Point(3, 3);
            this.searchControlPanel.Name = "searchControlPanel";
            this.searchControlPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.searchControlPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.searchControlPanel.Size = new System.Drawing.Size(670, 144);
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
            this.searchControlUpperPanel.Location = new System.Drawing.Point(3, 3);
            this.searchControlUpperPanel.Name = "searchControlUpperPanel";
            this.searchControlUpperPanel.Size = new System.Drawing.Size(664, 50);
            this.searchControlUpperPanel.TabIndex = 0;
            // 
            // advancedSearchCheckBox
            // 
            this.advancedSearchCheckBox.AutoSize = true;
            this.advancedSearchCheckBox.Location = new System.Drawing.Point(346, 27);
            this.advancedSearchCheckBox.Name = "advancedSearchCheckBox";
            this.advancedSearchCheckBox.Size = new System.Drawing.Size(112, 17);
            this.advancedSearchCheckBox.TabIndex = 26;
            this.advancedSearchCheckBox.Text = "&Advanced Search";
            this.advancedSearchCheckBox.UseVisualStyleBackColor = true;
            // 
            // limitResultDropDown
            // 
            this.limitResultDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.limitResultDropDown.FormattingEnabled = true;
            this.limitResultDropDown.Location = new System.Drawing.Point(464, 25);
            this.limitResultDropDown.Name = "limitResultDropDown";
            this.limitResultDropDown.Size = new System.Drawing.Size(124, 21);
            this.limitResultDropDown.TabIndex = 25;
            // 
            // searchButton
            // 
            this.searchButton.Location = new System.Drawing.Point(464, 0);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(96, 23);
            this.searchButton.TabIndex = 3;
            this.searchButton.Text = "&Search";
            this.searchButton.UseVisualStyleBackColor = true;
            // 
            // findLabel
            // 
            this.findLabel.AutoSize = true;
            this.findLabel.Location = new System.Drawing.Point(45, 28);
            this.findLabel.Name = "findLabel";
            this.findLabel.Size = new System.Drawing.Size(27, 13);
            this.findLabel.TabIndex = 4;
            this.findLabel.Text = "Find";
            // 
            // findComboBox
            // 
            this.findComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.findComboBox.FormattingEnabled = true;
            this.findComboBox.Location = new System.Drawing.Point(78, 25);
            this.findComboBox.Name = "findComboBox";
            this.findComboBox.Size = new System.Drawing.Size(111, 21);
            this.findComboBox.TabIndex = 5;
            // 
            // patternComboBox
            // 
            this.patternComboBox.Location = new System.Drawing.Point(78, 2);
            this.patternComboBox.Name = "patternComboBox";
            this.patternComboBox.Size = new System.Drawing.Size(262, 21);
            this.patternComboBox.TabIndex = 1;
            // 
            // whatToSearchComboBox
            // 
            this.whatToSearchComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.whatToSearchComboBox.FormattingEnabled = true;
            this.whatToSearchComboBox.Location = new System.Drawing.Point(195, 25);
            this.whatToSearchComboBox.Name = "whatToSearchComboBox";
            this.whatToSearchComboBox.Size = new System.Drawing.Size(145, 21);
            this.whatToSearchComboBox.TabIndex = 6;
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
            this.searchControlAdvancedPanel.Location = new System.Drawing.Point(3, 59);
            this.searchControlAdvancedPanel.Name = "searchControlAdvancedPanel";
            this.searchControlAdvancedPanel.Size = new System.Drawing.Size(664, 82);
            this.searchControlAdvancedPanel.TabIndex = 25;
            // 
            // fromDateCheckbox
            // 
            this.fromDateCheckbox.AutoSize = true;
            this.fromDateCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.fromDateCheckbox.Location = new System.Drawing.Point(29, 9);
            this.fromDateCheckbox.Name = "fromDateCheckbox";
            this.fromDateCheckbox.Size = new System.Drawing.Size(75, 17);
            this.fromDateCheckbox.TabIndex = 8;
            this.fromDateCheckbox.Text = "From Date";
            this.fromDateCheckbox.UseVisualStyleBackColor = true;
            // 
            // fromDateTimePicker
            // 
            this.fromDateTimePicker.CustomFormat = "yyyy/MM/dd";
            this.fromDateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.fromDateTimePicker.Location = new System.Drawing.Point(110, 6);
            this.fromDateTimePicker.Name = "fromDateTimePicker";
            this.fromDateTimePicker.Size = new System.Drawing.Size(103, 20);
            this.fromDateTimePicker.TabIndex = 9;
            // 
            // toDateCheckbox
            // 
            this.toDateCheckbox.AutoSize = true;
            this.toDateCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toDateCheckbox.Location = new System.Drawing.Point(39, 35);
            this.toDateCheckbox.Name = "toDateCheckbox";
            this.toDateCheckbox.Size = new System.Drawing.Size(65, 17);
            this.toDateCheckbox.TabIndex = 10;
            this.toDateCheckbox.Text = "To Date";
            this.toDateCheckbox.UseVisualStyleBackColor = true;
            // 
            // toDateTimePicker
            // 
            this.toDateTimePicker.CustomFormat = "yyyy/MM/dd";
            this.toDateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.toDateTimePicker.Location = new System.Drawing.Point(110, 32);
            this.toDateTimePicker.Name = "toDateTimePicker";
            this.toDateTimePicker.Size = new System.Drawing.Size(103, 20);
            this.toDateTimePicker.TabIndex = 11;
            // 
            // fromHourCheckbox
            // 
            this.fromHourCheckbox.AutoSize = true;
            this.fromHourCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.fromHourCheckbox.Location = new System.Drawing.Point(219, 9);
            this.fromHourCheckbox.Name = "fromHourCheckbox";
            this.fromHourCheckbox.Size = new System.Drawing.Size(75, 17);
            this.fromHourCheckbox.TabIndex = 12;
            this.fromHourCheckbox.Text = "From Hour";
            this.fromHourCheckbox.UseVisualStyleBackColor = true;
            // 
            // fromHourTimePicker
            // 
            this.fromHourTimePicker.CustomFormat = "HH:mm:ss";
            this.fromHourTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.fromHourTimePicker.Location = new System.Drawing.Point(300, 6);
            this.fromHourTimePicker.Name = "fromHourTimePicker";
            this.fromHourTimePicker.ShowUpDown = true;
            this.fromHourTimePicker.Size = new System.Drawing.Size(88, 20);
            this.fromHourTimePicker.TabIndex = 13;
            // 
            // toHourCheckbox
            // 
            this.toHourCheckbox.AutoSize = true;
            this.toHourCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toHourCheckbox.Location = new System.Drawing.Point(229, 35);
            this.toHourCheckbox.Name = "toHourCheckbox";
            this.toHourCheckbox.Size = new System.Drawing.Size(65, 17);
            this.toHourCheckbox.TabIndex = 14;
            this.toHourCheckbox.Text = "To Hour";
            this.toHourCheckbox.UseVisualStyleBackColor = true;
            // 
            // toHourTimePicker
            // 
            this.toHourTimePicker.CustomFormat = "HH:mm:ss";
            this.toHourTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.toHourTimePicker.Location = new System.Drawing.Point(300, 32);
            this.toHourTimePicker.Name = "toHourTimePicker";
            this.toHourTimePicker.ShowUpDown = true;
            this.toHourTimePicker.Size = new System.Drawing.Size(88, 20);
            this.toHourTimePicker.TabIndex = 15;
            // 
            // fromSizeCheckbox
            // 
            this.fromSizeCheckbox.AutoSize = true;
            this.fromSizeCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.fromSizeCheckbox.Location = new System.Drawing.Point(413, 9);
            this.fromSizeCheckbox.Name = "fromSizeCheckbox";
            this.fromSizeCheckbox.Size = new System.Drawing.Size(72, 17);
            this.fromSizeCheckbox.TabIndex = 16;
            this.fromSizeCheckbox.Text = "From Size";
            this.fromSizeCheckbox.UseVisualStyleBackColor = true;
            // 
            // fromSizeUpDown
            // 
            this.fromSizeUpDown.Location = new System.Drawing.Point(491, 6);
            this.fromSizeUpDown.Name = "fromSizeUpDown";
            this.fromSizeUpDown.Size = new System.Drawing.Size(69, 20);
            this.fromSizeUpDown.TabIndex = 17;
            // 
            // fromSizeDropDown
            // 
            this.fromSizeDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.fromSizeDropDown.FormattingEnabled = true;
            this.fromSizeDropDown.Location = new System.Drawing.Point(566, 6);
            this.fromSizeDropDown.Name = "fromSizeDropDown";
            this.fromSizeDropDown.Size = new System.Drawing.Size(75, 21);
            this.fromSizeDropDown.TabIndex = 18;
            // 
            // toSizeCheckbox
            // 
            this.toSizeCheckbox.AutoSize = true;
            this.toSizeCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toSizeCheckbox.Location = new System.Drawing.Point(423, 35);
            this.toSizeCheckbox.Name = "toSizeCheckbox";
            this.toSizeCheckbox.Size = new System.Drawing.Size(62, 17);
            this.toSizeCheckbox.TabIndex = 19;
            this.toSizeCheckbox.Text = "To Size";
            this.toSizeCheckbox.UseVisualStyleBackColor = true;
            // 
            // toSizeUpDown
            // 
            this.toSizeUpDown.Location = new System.Drawing.Point(491, 32);
            this.toSizeUpDown.Name = "toSizeUpDown";
            this.toSizeUpDown.Size = new System.Drawing.Size(69, 20);
            this.toSizeUpDown.TabIndex = 20;
            // 
            // toSizeDropDown
            // 
            this.toSizeDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.toSizeDropDown.FormattingEnabled = true;
            this.toSizeDropDown.Location = new System.Drawing.Point(566, 32);
            this.toSizeDropDown.Name = "toSizeDropDown";
            this.toSizeDropDown.Size = new System.Drawing.Size(75, 21);
            this.toSizeDropDown.TabIndex = 21;
            // 
            // notOlderThanCheckbox
            // 
            this.notOlderThanCheckbox.AutoSize = true;
            this.notOlderThanCheckbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.notOlderThanCheckbox.Location = new System.Drawing.Point(5, 58);
            this.notOlderThanCheckbox.Name = "notOlderThanCheckbox";
            this.notOlderThanCheckbox.Size = new System.Drawing.Size(99, 17);
            this.notOlderThanCheckbox.TabIndex = 22;
            this.notOlderThanCheckbox.Text = "Not Older Than";
            this.notOlderThanCheckbox.UseVisualStyleBackColor = true;
            // 
            // notOlderThanUpDown
            // 
            this.notOlderThanUpDown.Location = new System.Drawing.Point(110, 55);
            this.notOlderThanUpDown.Name = "notOlderThanUpDown";
            this.notOlderThanUpDown.Size = new System.Drawing.Size(79, 20);
            this.notOlderThanUpDown.TabIndex = 23;
            // 
            // notOlderThanDropDown
            // 
            this.notOlderThanDropDown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.notOlderThanDropDown.FormattingEnabled = true;
            this.notOlderThanDropDown.Location = new System.Drawing.Point(195, 54);
            this.notOlderThanDropDown.Name = "notOlderThanDropDown";
            this.notOlderThanDropDown.Size = new System.Drawing.Size(75, 21);
            this.notOlderThanDropDown.TabIndex = 24;
            // 
            // catalogTab
            // 
            this.catalogTab.Controls.Add(this.catalogResultPanel);
            this.catalogTab.Controls.Add(this.catalogControlPanel);
            this.catalogTab.Location = new System.Drawing.Point(4, 22);
            this.catalogTab.Name = "catalogTab";
            this.catalogTab.Padding = new System.Windows.Forms.Padding(3);
            this.catalogTab.Size = new System.Drawing.Size(676, 426);
            this.catalogTab.TabIndex = 1;
            this.catalogTab.Text = "Catalog";
            this.catalogTab.UseVisualStyleBackColor = true;
            // 
            // catalogResultPanel
            // 
            this.catalogResultPanel.Controls.Add(this.catalogResultListView);
            this.catalogResultPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.catalogResultPanel.Location = new System.Drawing.Point(3, 84);
            this.catalogResultPanel.Name = "catalogResultPanel";
            this.catalogResultPanel.Size = new System.Drawing.Size(670, 339);
            this.catalogResultPanel.TabIndex = 1;
            // 
            // catalogResultListView
            // 
            this.catalogResultListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.catalogResultListView.Location = new System.Drawing.Point(0, 0);
            this.catalogResultListView.Name = "catalogResultListView";
            this.catalogResultListView.Size = new System.Drawing.Size(670, 339);
            this.catalogResultListView.TabIndex = 0;
            this.catalogResultListView.UseCompatibleStateImageBehavior = false;
            // 
            // catalogControlPanel
            // 
            this.catalogControlPanel.Controls.Add(this.labelCatalogPlaceholder);
            this.catalogControlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.catalogControlPanel.Location = new System.Drawing.Point(3, 3);
            this.catalogControlPanel.Name = "catalogControlPanel";
            this.catalogControlPanel.Size = new System.Drawing.Size(670, 81);
            this.catalogControlPanel.TabIndex = 0;
            // 
            // labelCatalogPlaceholder
            // 
            this.labelCatalogPlaceholder.AutoSize = true;
            this.labelCatalogPlaceholder.Location = new System.Drawing.Point(304, 65);
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
            this.directoryTab.Size = new System.Drawing.Size(676, 426);
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
            this.directorySplitContainer.Size = new System.Drawing.Size(670, 393);
            this.directorySplitContainer.SplitterDistance = 222;
            this.directorySplitContainer.TabIndex = 0;
            // 
            // directoryListView
            // 
            this.directoryListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.directoryListView.Location = new System.Drawing.Point(0, 0);
            this.directoryListView.Name = "directoryListView";
            this.directoryListView.Size = new System.Drawing.Size(444, 393);
            this.directoryListView.TabIndex = 2;
            this.directoryListView.UseCompatibleStateImageBehavior = false;
            // 
            // directoryBottomPanel
            // 
            this.directoryBottomPanel.Controls.Add(this.copyPathButton);
            this.directoryBottomPanel.Controls.Add(this.directoryPathTextBox);
            this.directoryBottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.directoryBottomPanel.Location = new System.Drawing.Point(3, 396);
            this.directoryBottomPanel.Name = "directoryBottomPanel";
            this.directoryBottomPanel.Size = new System.Drawing.Size(670, 27);
            this.directoryBottomPanel.TabIndex = 1;
            // 
            // copyPathButton
            // 
            this.copyPathButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.copyPathButton.Location = new System.Drawing.Point(3, 3);
            this.copyPathButton.Name = "copyPathButton";
            this.copyPathButton.Size = new System.Drawing.Size(75, 23);
            this.copyPathButton.TabIndex = 2;
            this.copyPathButton.Text = "nothing";
            this.copyPathButton.UseVisualStyleBackColor = true;
            // 
            // directoryPathTextBox
            // 
            this.directoryPathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.directoryPathTextBox.Location = new System.Drawing.Point(84, 5);
            this.directoryPathTextBox.Name = "directoryPathTextBox";
            this.directoryPathTextBox.Size = new System.Drawing.Size(581, 20);
            this.directoryPathTextBox.TabIndex = 1;
            // 
            // logTab
            // 
            this.logTab.Controls.Add(this.listView1);
            this.logTab.Controls.Add(this.bottomLogPanel);
            this.logTab.Location = new System.Drawing.Point(4, 22);
            this.logTab.Name = "logTab";
            this.logTab.Size = new System.Drawing.Size(676, 426);
            this.logTab.TabIndex = 3;
            this.logTab.Text = "Log";
            this.logTab.UseVisualStyleBackColor = true;
            // 
            // listView1
            // 
            this.listView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listView1.Location = new System.Drawing.Point(0, 0);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(676, 397);
            this.listView1.TabIndex = 0;
            this.listView1.UseCompatibleStateImageBehavior = false;
            // 
            // bottomLogPanel
            // 
            this.bottomLogPanel.Controls.Add(this.logBottomPanelLabel);
            this.bottomLogPanel.Controls.Add(this.bottomLogButton);
            this.bottomLogPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomLogPanel.Location = new System.Drawing.Point(0, 397);
            this.bottomLogPanel.Name = "bottomLogPanel";
            this.bottomLogPanel.Size = new System.Drawing.Size(676, 29);
            this.bottomLogPanel.TabIndex = 2;
            // 
            // logBottomPanelLabel
            // 
            this.logBottomPanelLabel.AutoSize = true;
            this.logBottomPanelLabel.Location = new System.Drawing.Point(84, 8);
            this.logBottomPanelLabel.Name = "logBottomPanelLabel";
            this.logBottomPanelLabel.Size = new System.Drawing.Size(165, 13);
            this.logBottomPanelLabel.TabIndex = 2;
            this.logBottomPanelLabel.Text = "Newest Log messages are at top.";
            // 
            // bottomLogButton
            // 
            this.bottomLogButton.Location = new System.Drawing.Point(3, 3);
            this.bottomLogButton.Name = "bottomLogButton";
            this.bottomLogButton.Size = new System.Drawing.Size(75, 23);
            this.bottomLogButton.TabIndex = 1;
            this.bottomLogButton.Text = "nothing";
            this.bottomLogButton.UseVisualStyleBackColor = true;
            // 
            // mainStatusStrip
            // 
            this.mainStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.catalogsLoadedStatus,
            this.totalFileEntriesStatus,
            this.searchResultsStatus,
            this.searchTimeStatus,
            this.toolStripStatusLabelRightAlign});
            this.mainStatusStrip.Location = new System.Drawing.Point(0, 476);
            this.mainStatusStrip.Name = "mainStatusStrip";
            this.mainStatusStrip.ShowItemToolTips = true;
            this.mainStatusStrip.Size = new System.Drawing.Size(684, 24);
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
            this.catalogsLoadedStatus.Size = new System.Drawing.Size(66, 19);
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
            this.totalFileEntriesStatus.Size = new System.Drawing.Size(55, 19);
            this.totalFileEntriesStatus.Text = "Entries 0";
            this.totalFileEntriesStatus.ToolTipText = "Total File Entries in loaded catalogs";
            // 
            // searchResultsStatus
            // 
            this.searchResultsStatus.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) 
            | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.searchResultsStatus.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.searchResultsStatus.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.searchResultsStatus.Name = "searchResultsStatus";
            this.searchResultsStatus.Size = new System.Drawing.Size(95, 19);
            this.searchResultsStatus.Text = "Search Results 0";
            this.searchResultsStatus.ToolTipText = "Search Results Found (upto maximum)";
            // 
            // searchTimeStatus
            // 
            this.searchTimeStatus.Name = "searchTimeStatus";
            this.searchTimeStatus.Size = new System.Drawing.Size(12, 19);
            this.searchTimeStatus.Text = "*";
            this.searchTimeStatus.ToolTipText = "Search Time Taken msecs.";
            // 
            // toolStripStatusLabelRightAlign
            // 
            this.toolStripStatusLabelRightAlign.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusLabelRightAlign.Name = "toolStripStatusLabelRightAlign";
            this.toolStripStatusLabelRightAlign.Size = new System.Drawing.Size(410, 19);
            this.toolStripStatusLabelRightAlign.Spring = true;
            // 
            // CDEWinForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 500);
            this.Controls.Add(this.mainTabControl);
            this.Controls.Add(this.mainStatusStrip);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.MinimumSize = new System.Drawing.Size(400, 300);
            this.Name = "CDEWinForm";
            this.Text = "cdeWinView";
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
            this.bottomLogPanel.ResumeLayout(false);
            this.bottomLogPanel.PerformLayout();
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
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.Panel bottomLogPanel;
        private System.Windows.Forms.Button bottomLogButton;
        private System.Windows.Forms.Label logBottomPanelLabel;
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
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Util;
using cdeLib;

namespace cdeWin
{
	public partial class LoaderForm : Form
	{
		private readonly IConfig _config;

	    private List<RootEntry> _rootEntries;
		private readonly IEnumerable<string> _cdeList;
		private readonly TimeIt _timeIt;

		public LoaderForm(IConfig config, IEnumerable<string> cdeList, TimeIt timeIt)
		{
			InitializeComponent();
			_config = config;
			_cdeList = cdeList;
			_timeIt = timeIt;

			// Duplicate the App location if its set for this loader form
			_config.RestoreConfigFormTopLeft(this);

			AutoWaitCursor.Cursor = Cursors.WaitCursor;
			AutoWaitCursor.Delay = new TimeSpan(0, 0, 0, 0, 25);
			AutoWaitCursor.MainWindowHandle = Handle;
			AutoWaitCursor.Start();
		}

		private void btnOK_Click(object sender, System.EventArgs e)
		{
			Close();
		}

		private void LoaderForm_Shown(object sender, System.EventArgs e)
		{
			Application.DoEvents(); // Make sure controls render before we do something.
			// Not doing a real background thread for progress, this is simple as it is.
			Addline("{0} v{1}", _config.ProductName, _config.Version);
			var cacheFiles = RootEntry.GetCacheFileList(_cdeList);

			_rootEntries = cacheFiles.Select(s =>
			{
				//Addline("Loading {0}", s);
				_timeIt.Start(s);
				var re = RootEntry.LoadDirCache(s);
				var label = _timeIt.Stop();
				Addline("Catalog load {0} in {1} msec", label.Label, label.ElapsedMsec);
				return re;
			}).ToList();
			Addline("Total load time for {0} files was {1} msec", cacheFiles.Count, _timeIt.TotalMsec);

			if (AutoCloseLoaderFlag)
			{
				Close();
			}
		}

		private void Addline(string format, params object[] args)
		{
			tbLog.AppendText(string.Format(format, args) + Environment.NewLine);
		}

		public List<RootEntry> RootEntries
		{
			get { return _rootEntries; }
		}

		public bool AutoCloseLoaderFlag { 
			get { return cbAutoCloseLoader.Checked; } 
			set { cbAutoCloseLoader.Checked = value; } 
		}

	}
}

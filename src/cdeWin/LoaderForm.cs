using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Util;
using cdeLib;

namespace cdeWin
{
	public partial class LoaderForm : Form
	{
        private List<RootEntry> _rootEntries;
		private readonly IEnumerable<string> _cdeList;
		private readonly TimeIt _timeIt;

		public LoaderForm(IConfig config, IEnumerable<string> cdeList, TimeIt timeIt)
		{
            InitializeComponent();
            _cdeList = cdeList;
			_timeIt = timeIt;

			// Set to top left of application if it is available.
			config.RestoreConfigFormTopLeft(this);

			AutoWaitCursor.Cursor = Cursors.WaitCursor;
			AutoWaitCursor.Delay = new TimeSpan(0, 0, 0, 0, 25);
			AutoWaitCursor.MainWindowHandle = Handle;
			AutoWaitCursor.Start();
		}

        private void UpdateUI(Action action)
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
            Application.DoEvents(); // Get label to update.
        }

		private void LoaderForm_Shown(object sender, EventArgs e)
		{
            var repo = new CatalogRepository();
			lblProgressMessage.Text = string.Empty;
			Application.DoEvents(); // Make sure controls render before we do something.

			var cacheFiles = repo.GetCacheFileList(_cdeList);
			var totalFiles = cacheFiles.Count();
			var fileCounter = 0;
			barLoading.Step = totalFiles;
			barLoading.Maximum = totalFiles;

            var rootEntries = new ConcurrentStack<RootEntry>();
            Serilog.Log.Logger.Debug("Loading {catalogCount} catalogues", totalFiles);
            _timeIt.Start("Load Files");
            Parallel.ForEach(cacheFiles,
                (cacheFile) =>
            {
                var re = repo.LoadDirCache(cacheFile);
                Interlocked.Increment(ref fileCounter);
                rootEntries.Push(re);

                UpdateUI(() =>
                {
                    barLoading.Value = fileCounter;
                    lblProgressMessage.Text = $"Loading catalog {fileCounter} of {totalFiles}";
                });
            });

            Serilog.Log.Logger.Debug("Time to load catalogs {time}", _timeIt.TotalMsec);
            _rootEntries = rootEntries.ToList();
            Close();
		}

		public List<RootEntry> RootEntries => _rootEntries;
    }
}

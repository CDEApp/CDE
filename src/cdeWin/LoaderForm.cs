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

			// Set to top left of application if it is available.
			_config.RestoreConfigFormTopLeft(this);

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

		private void LoaderForm_Shown(object sender, System.EventArgs e)
		{
			lblProgressMessage.Text = string.Empty;
			Application.DoEvents(); // Make sure controls render before we do something.

			var cacheFiles = RootEntry.GetCacheFileList(_cdeList);
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
                var re = RootEntry.LoadDirCache(cacheFile);
                Interlocked.Increment(ref fileCounter);
                rootEntries.Push(re);

                UpdateUI(() =>
                {
                    barLoading.Value = fileCounter;
                    lblProgressMessage.Text = $"Loading catalog {fileCounter} of {totalFiles}";
                });
            });

//            int i = 1;
//            // Not doing a background worker.
//            			_rootEntries = cacheFiles.Select(s =>
//            			{
//            				//_timeIt.Start(s);
//            				var re = RootEntry.LoadDirCache(s);
//            				//_timeIt.Stop();
//            				++fileCounter;
//            				lblProgressMessage.Text = $"Loading catalog {fileCounter} of {totalFiles}";
//            				barLoading.Value = fileCounter;
//            				Application.DoEvents(); // Get label to update.
//            				return re;
//            			}).ToList();
//            _timeIt.Stop();

            Serilog.Log.Logger.Debug("Time to load catalogs {time}", _timeIt.TotalMsec);
            _rootEntries = rootEntries.ToList();
            Close();
		}

		public List<RootEntry> RootEntries => _rootEntries;
    }
}

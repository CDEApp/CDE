using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Util;
using cdeLib.Catalog;
using cdeLib.Entities;
using Dawn;
using Serilog;

namespace cdeWin
{
    public partial class LoaderForm : Form
    {
        private readonly IEnumerable<string> _cdeList;
        private readonly TimeIt _timeIt;
        private readonly ILogger _logger;
        private BackgroundWorker _backgroundWorker;

        public LoaderForm(IConfig config, IEnumerable<string> cdeList, TimeIt timeIt, ILogger logger)
        {
            InitializeComponent();
            InitializeBackgroundWorker();
            _cdeList = cdeList;
            _timeIt = timeIt;
            _logger = logger;

            // Set to top left of application if it is available.
            config.RestoreConfigFormTopLeft(this);

            AutoWaitCursor.Cursor = Cursors.WaitCursor;
            AutoWaitCursor.Delay = new TimeSpan(0, 0, 0, 0, 25);
            AutoWaitCursor.MainWindowHandle = Handle;
            AutoWaitCursor.Start();
        }

        private void InitializeBackgroundWorker()
        {
            _backgroundWorker.DoWork += BackgroundWorker_DoWork;
            _backgroundWorker.RunWorkerCompleted +=
                BackgroundWorker_RunWorkerCompleted;
            _backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the BackgroundWorker that raised this event.
            var worker = sender as BackgroundWorker;
            var catalogs = LoadCatalogs(worker);
            e.Result = catalogs;
            RootEntries = catalogs;
        }

        private void BackgroundWorker_RunWorkerCompleted(
            object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show($"There was an error loading catalogs {e.Error.Message}");
            }

            Close();
        }



        // This event handler updates the progress bar.
        private void BackgroundWorker_ProgressChanged(object sender,
            ProgressChangedEventArgs e)
        {
            var state = e.UserState as LoadingState;
            Guard.Argument(state, "state").NotNull();
            barLoading.Value = state.FileCount;
            lblProgressMessage.Text = $"Loading catalog {state.FileCount} of {state.TotalFiles}";
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
            lblProgressMessage.Text = string.Empty;
            Application.DoEvents(); // Make sure controls render before we do something.
            _backgroundWorker.RunWorkerAsync();
            _logger.Debug("Time to load catalogs {time}", _timeIt.TotalMsec);
        }

        public List<RootEntry> LoadCatalogs(BackgroundWorker worker)
        {
            var repo = new CatalogRepository(Log.Logger);
            var cacheFiles = repo.GetCacheFileList(_cdeList);
            var totalFiles = cacheFiles.Count;
            var fileCounter = 0;
            UpdateUI(() =>
            {
                barLoading.Step = 1;
                barLoading.Maximum = totalFiles;
            });

            var rootEntries = new ConcurrentStack<RootEntry>();

            try
            {
                Parallel.ForEach(cacheFiles,
                    (cacheFile) =>
                    {
                        var re = repo.LoadDirCache(cacheFile);
                        Interlocked.Increment(ref fileCounter);
                        rootEntries.Push(re);

                        worker.ReportProgress((int) ((float) fileCounter / (float) totalFiles * 100),
                            new LoadingState(fileCounter, totalFiles));
                    });
            }
            catch (AggregateException ex)
            {
                _logger.Error(ex, "Error loading catalogs");
                throw;
            }

            return rootEntries.ToList();
        }

        public List<RootEntry> RootEntries { get; private set; }
    }

    public class LoadingState
    {
        public LoadingState(int fileCount, int totalFiles)
        {
            FileCount = fileCount;
            TotalFiles = totalFiles;
        }

        public int FileCount { get; set; }
        public int TotalFiles { get; set; }
    }
}
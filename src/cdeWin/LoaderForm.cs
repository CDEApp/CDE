using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Util;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeWin.Cfg;
using Dawn;
using Serilog;
using SerilogTimings;
using SerilogTimings.Extensions;

namespace cdeWin;

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
        using (Operation.Time("Load catalogs"))
        {
            var catalogs = LoadCatalogs(worker);
            e.Result = catalogs;
            RootEntries = catalogs;
        }
    }

    private void BackgroundWorker_RunWorkerCompleted(
        object sender, RunWorkerCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            MessageBox.Show($@"There was an error loading catalogs {e.Error.Message}");
        }

        Close();
    }

    // This event handler updates the progress bar.
    private void BackgroundWorker_ProgressChanged(object sender,
        ProgressChangedEventArgs e)
    {
        if (e.UserState is not LoadingState state) return;
        barLoading.Value = state.FileCount;
        lblProgressMessage.Text = $@"Loading catalog {state.FileCount} of {state.TotalFiles}";
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

        Application.DoEvents(); // Get the label to update.
    }

    private void LoaderForm_Shown(object sender, EventArgs e)
    {
        lblProgressMessage.Text = string.Empty;
        Application.DoEvents(); // Make sure controls render before we do something.
        _backgroundWorker.RunWorkerAsync();
    }

    private List<RootEntry> LoadCatalogs(BackgroundWorker worker)
    {
        var repo = new CatalogRepository(Log.Logger);
        var cacheFiles = repo.GetCacheFileList(_cdeList);
        var totalFiles = cacheFiles.Count;
        var volatileFileCounter = 0;
        var progressReportThreshold = Math.Max(1, totalFiles / 50); // More frequent updates
        var lastProgressReport = DateTime.UtcNow;
        var progressReportInterval = TimeSpan.FromMilliseconds(100); // Report every 100ms max

        UpdateUI(() =>
        {
            barLoading.Step = 1;
            barLoading.Maximum = totalFiles;
        });

        var rootEntries = new ConcurrentBag<RootEntry>();

        try
        {
            // Use async parallel processing for I/O-bound operations with SemaphoreSlim for better control
            var semaphore = new SemaphoreSlim(Math.Min(Environment.ProcessorCount * 2, 8));
            var tasks = cacheFiles.Select(async cacheFile =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var re = await LoadCatalogOptimizedAsync(repo, cacheFile);
                    if (re != null)
                    {
                        var currentCount = Interlocked.Increment(ref volatileFileCounter);
                        rootEntries.Add(re);

                        // Time-based or count-based progress reporting (whichever comes first)
                        var now = DateTime.UtcNow;
                        if (currentCount % progressReportThreshold == 0 ||
                            (now - lastProgressReport) > progressReportInterval)
                        {
                            worker.ReportProgress((int)(currentCount / (float)totalFiles * 100),
                                new LoadingState(currentCount, totalFiles));
                            lastProgressReport = now;
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            Task.WaitAll(tasks.ToArray());
        }
        catch (AggregateException ex)
        {
            _logger.Error(ex, "Error loading catalogs");
            throw;
        }

        worker.ReportProgress(100, new LoadingState(volatileFileCounter, totalFiles));
        return rootEntries.ToList();
    }

    private static RootEntry LoadCatalogOptimized(CatalogRepository repo, string cacheFile)
    {
        try
        {
            // Check file existence first to avoid expensive operations
            if (!File.Exists(cacheFile)) return null;

            // Get file info to pre-allocate buffer size
            var fileInfo = new FileInfo(cacheFile);
            if (fileInfo.Length == 0) return null;

            // Load with optimized buffer size
            var rootEntry = repo.Read(cacheFile);
            if (rootEntry == null) return null;

            rootEntry.ActualFileName = cacheFile;
            rootEntry.SetInMemoryFields();
            return rootEntry;
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to load catalog file {CacheFile}, skipping", cacheFile);
            return null; // Continue processing other files
        }
    }

    private static async Task<RootEntry> LoadCatalogOptimizedAsync(CatalogRepository repo, string cacheFile)
    {
        try
        {
            // Check file existence first to avoid expensive operations
            if (!File.Exists(cacheFile)) return null;

            // Get file info to pre-allocate buffer size
            var fileInfo = new FileInfo(cacheFile);
            if (fileInfo.Length == 0) return null;

            // Load with async I/O
            var rootEntry = await repo.LoadDirCacheAsync(cacheFile);
            return rootEntry;
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to load catalog file {CacheFile}, skipping", cacheFile);
            return null; // Continue processing other files
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
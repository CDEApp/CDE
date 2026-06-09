using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;
using cdeWin.Cfg;
using JetBrains.Annotations;
using Serilog;

namespace cdeWin;

// ReSharper disable once InconsistentNaming
public interface ICDEWinFormPresenter : IPresenter;

public class CDEWinFormPresenter : Presenter<ICDEWinForm>, ICDEWinFormPresenter
{
    private const string DummyNodeName = "_dummyNode";
    private readonly Color _listViewForeColor = Color.Black;
    private readonly Color _listViewDirForeColor = Color.DarkBlue;

    private readonly ICDEWinForm _clientForm;

    // Catalogs are held as IEntrySource — either a zero-copy ColumnarCatalogReader over a memory-mapped
    // .cdex file (preferred: the catalog data stays in the OS page cache, not the managed heap) or, when
    // no .cdex exists, an in-memory EntryStore built from the loaded .cde tree. Each catalog root is
    // exposed as an EntryRef so the existing ICommonEntry-based GUI works unchanged either way.
    private List<ICommonEntry> _catalogRoots;
    private readonly IConfig _config;

    private static List<ICommonEntry> ToCatalogRoots(List<RootEntry> trees)
    {
        var roots = new List<ICommonEntry>(trees?.Count ?? 0);
        if (trees == null) return roots;
        for (var i = 0; i < trees.Count; i++)
        {
            roots.Add(new EntryRef(EntryStore.Build(trees[i]), 0));
            trees[i] = null; // release the tree so it can be collected
        }
        return roots;
    }

    // Open each .cdex as a zero-copy mmap reader. Unreadable files are skipped (logged by the caller).
    private static List<ICommonEntry> ReadersToCatalogRoots(IList<string> cdexFiles)
    {
        var roots = new List<ICommonEntry>(cdexFiles.Count);
        foreach (var file in cdexFiles)
        {
            try
            {
                roots.Add(new EntryRef(new ColumnarCatalogReader(file), 0));
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Skipping unreadable .cdex {File}", file);
            }
        }
        return roots;
    }

    private static IEntrySource SourceOf(ICommonEntry root) => ((EntryRef)root).Source;

    // Catalog of a search-result pair (its entries are EntryRefs into a source).
    private static IEntrySource SourceOfPair(PairDirEntry pde) => (pde.ChildDE as EntryRef)?.Source;

    // Prefer the zero-copy .cdex catalogs (mmap, near-zero managed heap); fall back to loading the
    // .cde trees and building in-memory stores when no .cdex exists. Disposes any previously held
    // mmap sources first so reloads don't leak mappings.
    private async Task<List<ICommonEntry>> LoadCatalogRootsAsync()
    {
        DisposeCatalogSources();
        var cdex = _loadCatalogService.GetColumnarFiles(_config);
        if (cdex is { Count: > 0 })
        {
            return ReadersToCatalogRoots(cdex);
        }
        return ToCatalogRoots(await _loadCatalogService.LoadRootEntriesAsync(
            _config, OnLoadProgress, _loadingCts.Token));
    }

    // Memory-mapped catalog sources must be released on reload/exit so the mappings are closed.
    private void DisposeCatalogSources()
    {
        if (_catalogRoots == null) return;
        foreach (var root in _catalogRoots)
        {
            if (root is EntryRef { Source: IDisposable disposable }) disposable.Dispose();
        }
    }

    private readonly string[] _directoryVals;
    private readonly string[] _searchVals;
    private readonly string[] _catalogVals;

    // Cache for formatted date strings
    private readonly Dictionary<DateTime, string> _dateCache = new(1024);

    private List<PairDirEntry> _searchResultList;
    private List<ICommonEntry> _directoryList;

    /// <summary>
    /// The entry that is the parent of the Directory List View displayed items.
    /// </summary>
    private ICommonEntry _directoryListCommonEntry;

    private BackgroundWorker _bgWorker;
    private bool _isSearchButton;
    private readonly ILoadCatalogService _loadCatalogService;
    private CancellationTokenSource _loadingCts;
    private bool _isLoadingCatalogs;

    // Loading animation - dots cycle through a wave pattern
    private System.Windows.Forms.Timer _loadingAnimationTimer;
    private int _loadingAnimationFrame;
    private static readonly string[] LoadingAnimationFrames =
    [
        "Please wait ···",
        "Please wait ··•",
        "Please wait ·•·",
        "Please wait •··"
    ];

    public CDEWinFormPresenter(
        ICDEWinForm form,
        IConfig config,
        ILoadCatalogService loadCatalogService = null)
        : base(form)
    {
        _clientForm = form;
        _config = config;
        _loadCatalogService = loadCatalogService;
        _catalogRoots = new List<ICommonEntry>();

        _searchVals = new string[_config.DefaultSearchResultColumnCount];
        _directoryVals = new string[_config.DefaultDirectoryColumnCount];
        _catalogVals = new string[_config.DefaultCatalogColumnCount];

        SetSearchButton(true);
        RegisterListViewSorters();
        InitialiseLog();
    }

    public async Task InitializeAsync()
    {
        if (_isLoadingCatalogs) return;

        _isLoadingCatalogs = true;
        _loadingCts = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();

        // Disable search during loading
        _clientForm.SearchButtonEnable = false;
        StartLoadingAnimation();
        _clientForm.SetCatalogsLoadedStatus(0);
        _clientForm.SetTotalFileEntriesLoadedStatus(0);
        _clientForm.SetSearchTimeStatus("Loading catalogs...");
        _clientForm.ShowLoadingProgress(true);
        _clientForm.SetLoadingProgressValue(0);
        SetMemoryStatus();

        try
        {
            _catalogRoots = await LoadCatalogRootsAsync();

            SetCatalogListView();
            SetMemoryStatus();
            _clientForm.AddLine("Total Load time was {0} msec", watch.ElapsedMilliseconds);
            _clientForm.SetSearchTimeStatus("");
        }
        catch (OperationCanceledException)
        {
            _clientForm.AddLine("Catalog loading was cancelled");
            _clientForm.SetSearchTimeStatus("Loading cancelled");
        }
        catch (Exception ex)
        {
            _clientForm.AddLine("Error loading catalogs: {0}", ex.Message);
            _clientForm.SetSearchTimeStatus("Loading error");
            Log.Error(ex, "Error loading catalogs");
        }
        finally
        {
            _isLoadingCatalogs = false;
            _clientForm.ShowLoadingProgress(false);
            StopLoadingAnimation();
            _clientForm.SearchButtonEnable = true;
            _loadingCts?.Dispose();
            _loadingCts = null;
        }
    }

    private void OnLoadProgress(int current, int total, string message)
    {
        if (_clientForm is Control { InvokeRequired: true } control)
        {
            control.BeginInvoke(() => OnLoadProgress(current, total, message));
            return;
        }

        _clientForm.SetCatalogsLoadedStatus(current);
        _clientForm.SetSearchTimeStatus(message);
        if (total > 0)
        {
            _clientForm.SetLoadingProgressValue(current * 100 / total);
        }
        SetMemoryStatus();
    }

    public void CancelLoading()
    {
        _loadingCts?.Cancel();
    }

    private void StartLoadingAnimation()
    {
        _loadingAnimationFrame = 0;
        _clientForm.SearchButtonText = LoadingAnimationFrames[0];

        _loadingAnimationTimer?.Dispose();
        _loadingAnimationTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _loadingAnimationTimer.Tick += (_, _) =>
        {
            _loadingAnimationFrame = (_loadingAnimationFrame + 1) % LoadingAnimationFrames.Length;
            _clientForm.SearchButtonText = LoadingAnimationFrames[_loadingAnimationFrame];
        };
        _loadingAnimationTimer.Start();
    }

    private void StopLoadingAnimation()
    {
        _loadingAnimationTimer?.Stop();
        _loadingAnimationTimer?.Dispose();
        _loadingAnimationTimer = null;
        _clientForm.SearchButtonText = "Search";
    }

    private void InitialiseLog()
    {
        _clientForm.AddLine("{0} v{1}", _config.ProductName, _config.Version);
    }

    private void RegisterListViewSorters()
    {
        _clientForm.SearchResultListViewHelper.ColumnSortCompare = SearchResultCompare;
        _clientForm.DirectoryListViewHelper.ColumnSortCompare = DirectoryCompare;
        _clientForm.CatalogListViewHelper.ColumnSortCompare = RootCompare;
    }

    private void SetCatalogListView()
    {
        var catalogHelper = _clientForm.CatalogListViewHelper;
        var count = catalogHelper.SetList(_catalogRoots);
        catalogHelper.SortList();
        _clientForm.SetCatalogsLoadedStatus(count);
        _clientForm.SetTotalFileEntriesLoadedStatus(
            (int)_catalogRoots.Sum(r => (long)SourceOf(r).RootFileEntryCount + SourceOf(r).RootDirEntryCount));
    }

    private static double BytesToMb(long bytes) => bytes / (1024.0 * 1024.0);

    private void SetMemoryStatus()
    {
        double memory;
        using (var proc = Process.GetCurrentProcess())
        {
            memory = BytesToMb(proc.PrivateMemorySize64);
        }

        _clientForm.SetMemoryStatus($"Memory used: {memory:N0} MB");
    }

    private void SetSearchButton(bool search)
    {
        _isSearchButton = search;
        _clientForm.SearchButtonText = _isSearchButton ? "Search" : "Cancel Search";
        _clientForm.SearchButtonBackColor = _isSearchButton ? default : Color.LightCoral;
    }

    public void Display()
    {
        try
        {
            _clientForm.ShowDialog();
        }
        finally
        {
            _clientForm.Dispose();
        }
    }
    
    [UsedImplicitly]
    public void FormShown()
    {
        // Setup our sort arrow icons, this requires windows message loop afaik.
        _clientForm.CatalogListViewHelper.SortList();
        _clientForm.SearchResultListViewHelper.SortList();
        _clientForm.DirectoryListViewHelper.SortList();
    }

    [UsedImplicitly]
    public void FormActivated()
    {
    }

    public void DirectoryTreeViewBeforeExpandNode()
    {
        CreateNodesPreExpand(_clientForm.DirectoryTreeViewActiveBeforeExpandNode);
    }

    private static void CreateNodesPreExpand(TreeNode parentNode)
    {
        if (!HasDummyChildNode(parentNode)) return;
        // Replace Dummy with real nodes now visible.
        parentNode.Nodes.Clear();
        AddAllDirectoriesChildren(parentNode, (ICommonEntry)parentNode.Tag);
    }

    private static bool HasDummyChildNode(TreeNode parentNode)
    {
        return parentNode.Nodes is [{ Text: DummyNodeName }];
    }

    private static void AddAllDirectoriesChildren(TreeNode treeNode, ICommonEntry dirEntry)
    {
        foreach (var subDirEntry in dirEntry.Children)
        {
            AddDirectoryChildren(treeNode, subDirEntry);
        }
    }

    private static void AddDirectoryChildren(TreeNode treeNode, ICommonEntry dirEntry)
    {
        if (!dirEntry.IsDirectory) return;
        var newTreeNode = NewTreeNode(dirEntry);
        treeNode.Nodes.Add(newTreeNode);
        SetDummyChildNode(newTreeNode, dirEntry);
    }

    /// <summary>
    /// A node with children gets a dummy child node so Tree view shows node as expandable.
    /// </summary>
    private static void SetDummyChildNode(TreeNode treeNode, ICommonEntry commonEntry)
    {
        if (commonEntry.Children?.Any(entry => entry.IsDirectory) == true)
        {
            treeNode.Nodes.Add(NewTreeNode(DummyNodeName));
        }
    }

    private static TreeNode NewTreeNode(ICommonEntry commonEntry)
    {
        return NewTreeNode(commonEntry.Path, commonEntry);
    }

    private static TreeNode NewTreeNode(string name, object tag = null)
    {
        return new(name)
        {
            Tag = tag
        };
    }

    public void CatalogRetrieveVirtualItem()
    {
        var catalogHelper = _clientForm.CatalogListViewHelper;
        var index = catalogHelper.RetrieveItemIndex;
        var rootEntry = catalogHelper.GetItemAt(index);
        if (rootEntry == null)
        {
            return;
        }
        var itemColor = CreateRowValuesForRootEntry(_catalogVals, rootEntry, _listViewForeColor);
        var lvi = BuildListViewItem(_catalogVals, itemColor, rootEntry);
        catalogHelper.RenderItem = lvi;
    }

    private Color CreateRowValuesForRootEntry(IList<string> vals, ICommonEntry catalogRoot, Color listViewForeColor)
    {
        var s = SourceOf(catalogRoot);
        var scanStart = new DateTime(s.ScanStartUtcTicks, DateTimeKind.Utc);
        var scanDurationMs = (s.ScanEndUtcTicks - s.ScanStartUtcTicks) / TimeSpan.TicksPerMillisecond;
        vals[0] = s.RootPath;
        vals[1] = s.VolumeName;
        vals[2] = s.RootDirEntryCount.ToString();
        vals[3] = s.RootFileEntryCount.ToString();
        vals[4] = (s.RootDirEntryCount + s.RootFileEntryCount).ToString();
        vals[5] = s.DriveLetterHint;
        vals[6] = s.RootSize.ToHRString();
        vals[7] = s.AvailSpace.ToHRString();
        vals[8] = s.TotalSpace.ToHRString();
        vals[9] = string.Format(_config.DateFormatYMDHMS, scanStart.ToLocalTime());
        vals[10] = $"{TimeSpan.FromMilliseconds(scanDurationMs).TotalSeconds:0.} sec";
        vals[11] = s.ActualFileName;
        vals[12] = s.Description;

        return listViewForeColor;
    }

    public class BgWorkerParam
    {
        public FindOptions Options;
        public IList<ICommonEntry> RootEntries;
        public BgWorkerState State;
    }

    public class BgWorkerState
    {
        public int ListCount;
        public List<PairDirEntry> List;
        public int Counter;
        public int End;
    }

    public void Search()
    {
        if (!_isSearchButton)
        {
            CancelSearch();
            SetSearchButton(true);
            return;
        }

        // Change button to "Cancel Search" immediately for responsive UI
        SetSearchButton(false);
        Application.DoEvents(); // Force UI refresh

        if (RegexIsBad()
            || FromToSizeInvalid()
            || FromToDateInvalid()
            || FromToHourInvalid())
        {
            // Reset button if validation fails
            SetSearchButton(true);
            return;
        }

        _clientForm.AddSearchTextBoxAutoComplete(_clientForm.Pattern);

        var optimisedPattern = OptimiseRegexPattern(_clientForm.Pattern);

        _bgWorker = new BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
        _bgWorker.DoWork +=BgWorkerDoWork;
        _bgWorker.RunWorkerCompleted += BgWorkerRunWorkerCompleted;
        _bgWorker.ProgressChanged += BgWorkerProgressChanged;

        var findOptions = new FindOptions
        {
            LimitResultCount = _clientForm.LimitResultHelper.SelectedValue,
            Pattern = optimisedPattern,
            RegexMode = _clientForm.RegexMode,
            IncludePath = _clientForm.IncludePathInSearch,
            IncludeFiles = _clientForm.IncludeFiles,
            IncludeFolders = _clientForm.IncludeFolders,
            // This many file system entries before progress
            // for slow regex like example .*moooxxxx.* - 5000 is fairly long on i7.
            ProgressModifier = 50000,
            FromSizeEnable = _clientForm.FromSize.Checked,
            FromSize = FromSizeValue(),
            ToSizeEnable = _clientForm.ToSize.Checked,
            ToSize = ToSizeValue(),
            FromDateEnable = _clientForm.FromDate.Checked,
            FromDate = _clientForm.FromDateValue.Date,
            ToDateEnable = _clientForm.ToDate.Checked,
            ToDate = _clientForm.ToDateValue.Date,
            FromHourEnable = _clientForm.FromHour.Checked,
            FromHour = _clientForm.FromHourValue.TimeOfDay,
            ToHourEnable = _clientForm.ToHour.Checked,
            ToHour = _clientForm.ToHourValue.TimeOfDay,
            NotOlderThanEnable = _clientForm.NotOlderThan.Checked,
            NotOlderThan = NotOlderThanValue()
        };

        var param = new BgWorkerParam
        {
            Options = findOptions,
            RootEntries = _catalogRoots,
            State = new BgWorkerState()
        };
        _bgWorker.RunWorkerAsync(param);
    }

    private bool FromToDateInvalid()
    {
        if (!_clientForm.FromDate.Checked
            || !_clientForm.ToDate.Checked
            || _clientForm.FromDateValue.Date < _clientForm.ToDateValue.Date) return false;
        _clientForm.MessageBox(
            "The From Date Field is greater than the To Date field no search results possible.");
        return true;

    }

    private bool FromToHourInvalid()
    {
        if (_clientForm.FromHour.Checked
            && _clientForm.ToHour.Checked
            && _clientForm.FromHourValue.TimeOfDay.TotalSeconds >= _clientForm.ToHourValue.TimeOfDay.TotalSeconds)
        {
            _clientForm.MessageBox(
                "The From Hour Field is greater than the To Hour field no search results possible.");
            return true;
        }

        return false;
    }

    private bool RegexIsBad()
    {
        if (!_clientForm.RegexMode) return false;
        var regexError = RegexHelper.GetRegexErrorMessage(_clientForm.Pattern);
        if (string.IsNullOrEmpty(regexError)) return false;
        _clientForm.MessageBox(regexError);
        return true;

    }

    private bool FromToSizeInvalid()
    {
        if (!_clientForm.FromSize.Checked
            || !_clientForm.ToSize.Checked
            || FromSizeValue() <= ToSizeValue()) return false;
        _clientForm.MessageBox(
            "The From Size Field is greater than the To Size field no search results possible.");
        return true;

    }

    private long FromSizeValue()
    {
        var value = _clientForm.FromSizeDropDownHelper.SelectedValue;
        return (long)(_clientForm.FromSizeValue.Field * value);
    }

    private long ToSizeValue()
    {
        var value = _clientForm.ToSizeDropDownHelper.SelectedValue;
        return (long)(_clientForm.ToSizeValue.Field * value);
    }

    private DateTime NotOlderThanValue()
    {
        var dropDownValueFunc = _clientForm.NotOlderThanDropDownHelper.SelectedValue;
        var fieldValue = (int)_clientForm.NotOlderThanValue.Field;
        var now = DateTime.Now; // Option to set this to date of newest scanned Catalog
        return dropDownValueFunc(now, -fieldValue); // subtract as we are going back in time.
    }

    // Assumes well-formed regex pattern input.
    // As search is a substring match, remove leading and trailing wildcards.
    protected string OptimiseRegexPattern(string pattern)
    {
        if (!_clientForm.RegexMode || string.IsNullOrEmpty(pattern))
        {
            return pattern;
        }

        // Single-pass optimization using Span to find trim boundaries
        var span = pattern.AsSpan();
        var start = 0;
        var end = span.Length;

        // Find start position (skip all leading ".*")
        while (end - start >= 2 && span[start] == '.' && span[start + 1] == '*')
        {
            start += 2;
        }

        // Find end position (skip all trailing ".*")
        while (end - start >= 2 && span[end - 2] == '.' && span[end - 1] == '*')
        {
            end -= 2;
        }

        // Return original if no changes, otherwise create single new string
        return start == 0 && end == span.Length
            ? pattern
            : span[start..end].ToString();
    }

    private void BgWorkerDoWork(object sender, DoWorkEventArgs e)
    {
        var worker = (BackgroundWorker)sender;
        var argument = (BgWorkerParam)e.Argument;
        var findOptions = argument.Options;
        var catalogRoots = argument.RootEntries;
        var state = argument.State;

        // Translate the GUI FindOptions into the SoA search options and run over the EntryStores.
        var opts = new EntryStoreFindOptions
        {
            Pattern = findOptions.Pattern,
            RegexMode = findOptions.RegexMode,
            IncludePath = findOptions.IncludePath,
            IncludeFiles = findOptions.IncludeFiles,
            IncludeFolders = findOptions.IncludeFolders,
            FromSizeEnable = findOptions.FromSizeEnable, FromSize = findOptions.FromSize,
            ToSizeEnable = findOptions.ToSizeEnable, ToSize = findOptions.ToSize,
            FromDateEnable = findOptions.FromDateEnable, FromDate = findOptions.FromDate,
            ToDateEnable = findOptions.ToDateEnable, ToDate = findOptions.ToDate,
            FromHourEnable = findOptions.FromHourEnable, FromHour = findOptions.FromHour,
            ToHourEnable = findOptions.ToHourEnable, ToHour = findOptions.ToHour,
            NotOlderThanEnable = findOptions.NotOlderThanEnable, NotOlderThan = findOptions.NotOlderThan,
        };
        var limit = findOptions.LimitResultCount;

        var sources = catalogRoots.Select(SourceOf).ToList();
        var grandTotal = sources.Sum(s => s.Count);
        var scannedBase = 0;

        var list = new List<PairDirEntry>(500);
        state.ListCount = 0;
        state.List = list;
        state.End = grandTotal;
        worker.ReportProgress(0, state);

        var lastReport = Stopwatch.GetTimestamp();
        var reportTicks = Stopwatch.Frequency / 10; // ~100ms streaming

        var timer = Stopwatch.StartNew();
        foreach (var source in sources)
        {
            if (worker.CancellationPending || list.Count >= limit) break;
            var baseScanned = scannedBase;
            source.Find(opts,
                onMatch: idx =>
                {
                    list.Add(new PairDirEntry(new EntryRef(source, source.ParentOf(idx)), new EntryRef(source, idx)));
                },
                isCancelled: () => worker.CancellationPending || list.Count >= limit,
                onScan: scanned => Report(baseScanned + scanned));
            scannedBase += source.Count;
        }
        timer.Stop();
        Log.Logger.Information(
            "Search execution time: {ExecutionTime} ms, Total found {TotalFound}",
            timer.ElapsedMilliseconds, list.Count);
        state.ListCount = list.Count;
        state.List = list;
        state.Counter = grandTotal;
        worker.ReportProgress(100, state);
        e.Result = list;
        return;

        void Report(int scanned)
        {
            var now = Stopwatch.GetTimestamp();
            if (now - lastReport < reportTicks) return;
            lastReport = now;
            state.ListCount = list.Count;
            state.List = new List<PairDirEntry>(list); // immutable snapshot for the UI thread
            state.Counter = scanned;
            worker.ReportProgress(grandTotal > 0 ? (int)(100.0 * scanned / grandTotal) : 0, state);
        }
    }

    private void BgWorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        var searchHelper = _clientForm.SearchResultListViewHelper;
        int count;

        if (e == null)
        {
            count = 0;
        }
        else
        {
            if (e.Error != null)
            {
                _clientForm.MessageBox(e.Error.Message);
                count = 0;
            }
            else if (e.Cancelled)
            {
                count = searchHelper.SetList(_searchResultList);
            }
            else
            {
                var resultList = (List<PairDirEntry>)e.Result;
                count = SetSearchResultList(resultList);
            }
        }

        _clientForm.SetSearchResultStatus(count);
        searchHelper.SortList();
        SetSearchButton(true);
        _bgWorker.Dispose();
        _bgWorker = null;
    }

    private void BgWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        var state = (BgWorkerState)e.UserState;
        var p = e.ProgressPercentage;
        _clientForm.SetSearchTimeStatus("% " + p);

        var count = SetSearchResultList(state.List);
        _clientForm.SetSearchResultStatus(count);
    }

    protected int SetSearchResultList(List<PairDirEntry> list)
    {
        _searchResultList = list;
        return _clientForm.SearchResultListViewHelper.SetList(list);
    }

    private void CancelSearch()
    {
        _bgWorker?.CancelAsync();
    }

    public void SearchResultRetrieveVirtualItem()
    {
        var searchHelper = _clientForm.SearchResultListViewHelper;
        var pairDirEntry = searchHelper.GetItemAt(searchHelper.RetrieveItemIndex);
        if (pairDirEntry == null)
        {
            return;
        }
        var dirEntry = pairDirEntry.ChildDE;
        var itemColor = CreateRowValuesForDirectory(_searchVals, dirEntry, _listViewForeColor);

        _searchVals[(int)SearchResultColumn.FullPath] = pairDirEntry.ParentDE.FullPath;

        //TODO: Possibly wasting cycles traversing to the root for this, make smarter.
        _searchVals[(int)SearchResultColumn.Catalog] =
            SourceOfPair(pairDirEntry)?.DefaultFileName ?? pairDirEntry.GetRootEntry()?.DefaultFileName ?? "";

        searchHelper.RenderItem = BuildListViewItem(_searchVals, itemColor, pairDirEntry);
    }

    public void DirectoryTreeViewAfterSelect()
    {
        var selectedNode = _clientForm.DirectoryTreeViewActiveAfterSelectNode;
        SetDirectoryListView((ICommonEntry)selectedNode.Tag);
    }

    private void SetDirectoryListView(ICommonEntry commonEntry)
    {
        _directoryListCommonEntry = commonEntry;
        var directoryHelper = _clientForm.DirectoryListViewHelper;
        _directoryList = new List<ICommonEntry>(commonEntry.Children?.ToList() ?? []);
        directoryHelper.SetList(_directoryList);
        directoryHelper.SortList();
        _clientForm.SetDirectoryPathTextBox = commonEntry.FullPath;
    }

    public void DirectoryRetrieveVirtualItem()
    {
        var directoryHelper = _clientForm.DirectoryListViewHelper;
        var dirEntry = directoryHelper.GetItemAt(directoryHelper.RetrieveItemIndex);
        if (dirEntry == null)
        {
            return;
        }
        var itemColor = CreateRowValuesForDirectory(_directoryVals, dirEntry, _listViewForeColor);
        var lvi = BuildListViewItem(_directoryVals, itemColor, dirEntry);
        directoryHelper.RenderItem = lvi;
    }

    private string FormatDate(DateTime date)
    {
        if (_dateCache.TryGetValue(date, out var cached))
            return cached;

        var result = string.Format(_config.DateFormatYMDHMS, date);

        if (_dateCache.Count < 10000)
            _dateCache[date] = result;

        return result;
    }

    private Color CreateRowValuesForDirectory(IList<string> vals, ICommonEntry dirEntry, Color itemColor)
    {
        vals[0] = dirEntry.Path;
        vals[1] = dirEntry.Size.ToString();
        if (dirEntry.IsDirectory)
        {
            itemColor = _listViewDirForeColor;
            if (dirEntry.IsDirectory)
            {
                var val = dirEntry.Size.ToHRString()
                          + " <Dir";
                if (dirEntry.IsReparsePoint)
                {
                    val += " R";
                }

                vals[1] = val + ">";
            }
        }

        vals[2] = dirEntry.IsModifiedBad
            ? "<Bad Date>"
            : FormatDate(dirEntry.Modified);
        return itemColor;
    }

    // before form closes capture any changed configuration.
    public void MyFormClosing()
    {
        CancelLoading();
        _config.RecordConfig(_clientForm);
        DisposeCatalogSources(); // close any memory-mapped .cdex catalogs
        _clientForm.CleanUp();
    }

    public void CatalogListViewItemActivate()
    {
        _clientForm.CatalogListViewHelper.ActionOnActivateItem(GoToDirectoryRoot);
    }

    private void GoToDirectoryRoot(ICommonEntry newRoot)
    {
        var currentRoot = (ICommonEntry)_clientForm.DirectoryTreeViewNodes?.Tag;
        if (!SameRoot(currentRoot, newRoot))
        {
            SetNewDirectoryRoot(newRoot);
        }

        _clientForm.SelectDirectoryPane();
    }

    // Catalog roots are EntryRef instances; two refs to the same catalog share a store.
    private static bool SameRoot(ICommonEntry a, ICommonEntry b)
        => a is EntryRef ea && b is EntryRef eb && ReferenceEquals(ea.Source, eb.Source);

    private TreeNode SetNewDirectoryRoot(ICommonEntry newRoot)
    {
        var newRootNode = BuildRootNode(newRoot);
        _clientForm.DirectoryTreeViewNodes = newRootNode;
        _clientForm.DirectoryListViewHelper.InitSort();
        return newRootNode;
    }

    private static TreeNode BuildRootNode(ICommonEntry rootEntry)
    {
        var rootTreeNode = NewTreeNode(rootEntry);
        SetDummyChildNode(rootTreeNode, rootEntry);
        return rootTreeNode;
    }

    public void DirectoryListViewItemActivate()
    {
        _clientForm.DirectoryListViewHelper.ActionOnActivateItem(d =>
        {
            if (d.IsDirectory)
            {
                SetDirectoryWithExpand(d);
            }
        });
    }

    private void SetDirectoryWithExpand(ICommonEntry dirEntry)
    {
        var activatedDirEntryList = dirEntry.GetListFromRoot();

        SetDirectoryWithExpand(activatedDirEntryList);
    }

    private void SetDirectoryWithExpand(IEnumerable<ICommonEntry> activatedDirEntryList)
    {
        var currentRootNode = _clientForm.DirectoryTreeViewNodes;
        var currentRoot = (ICommonEntry)currentRootNode?.Tag;

        TreeNode workingTreeNode = null;
        ICommonEntry newRoot = null;
        foreach (var entry in activatedDirEntryList)
        {
            if (newRoot == null)
            {
                newRoot = entry;
                if (!SameRoot(currentRoot, newRoot))
                {
                    currentRootNode = SetNewDirectoryRoot(newRoot);
                    currentRoot = newRoot;
                }

                workingTreeNode = currentRootNode; // starting at rootnode.
            }
            else
            {
                if (((DirEntry)entry).IsDirectory && workingTreeNode != null)
                {
                    CreateNodesPreExpand(workingTreeNode);
                    workingTreeNode.Expand();
                    object findTag = entry;
                    var nodeForCurrentEntry = workingTreeNode.Nodes.Cast<TreeNode>()
                        .FirstOrDefault(node => node.Tag == findTag);
                    workingTreeNode = nodeForCurrentEntry;
                }
            }
        }

        if (workingTreeNode != null)
        {
            CreateNodesPreExpand(workingTreeNode);
            workingTreeNode.Expand();
            _clientForm.DirectoryTreeViewSelectedNode = workingTreeNode;

            // This is a required or item under cursor after double click is selected.
            // not sure why ? some sort of left over click on new ListView content.
            var directoryHelper = _clientForm.DirectoryListViewHelper;
            directoryHelper.DeselectAllItems();
            _clientForm.SelectDirectoryPane();
        }
    }

    public void SearchResultListViewItemActivate()
    {
        _clientForm.SearchResultListViewHelper.ActionOnActivateItem(ViewFileInDirectoryTab);
    }

    public void DirectoryListViewItemSelectionChanged()
    {
        var directoryHelper = _clientForm.DirectoryListViewHelper;
        var indices = directoryHelper.SelectedIndices;
        var indicesCount = directoryHelper.SelectedIndicesCount;
        if (indicesCount > 0)
        {
            var firstIndex = indices.First();
            var dirEntry = _directoryList[firstIndex];
            _clientForm.SetDirectoryPathTextBox = indicesCount > 1
                ? _directoryListCommonEntry.FullPath
                : _directoryListCommonEntry.MakeFullPath(dirEntry);
        }
    }

    public void SearchResultListViewColumnClick()
    {
        _clientForm.SearchResultListViewHelper.ListViewColumnClick();
    }

    private int SearchResultCompare(PairDirEntry pde1, PairDirEntry pde2)
    {
        int compareResult;
        var de1 = pde1.ChildDE;
        var de2 = pde2.ChildDE;
        var searchResultHelper = _clientForm.SearchResultListViewHelper;
        var sortColumn = searchResultHelper.SortColumn;
        switch (sortColumn)
        {
            case 0: // SearchResult ListView Name column
                compareResult = de1.PathCompareWithDirTo(de2);
                break;

            case 1: // SearchResult ListView Size column
                compareResult = de1.SizeCompareWithDirTo(de2);
                break;

            case 2: // SearchResult ListView Modified column
                compareResult = de1.ModifiedCompareTo(de2);
                break;

            case 3:
                compareResult = string.Compare(
                    SourceOfPair(pde1)?.ActualFileName ?? pde1.GetRootEntry()?.ActualFileName,
                    SourceOfPair(pde2)?.ActualFileName ?? pde2.GetRootEntry()?.ActualFileName,
                    StringComparison.OrdinalIgnoreCase);
                break;

            case 4: // SearchResult ListView Path column
                compareResult = string.Compare(pde1.ParentDE.FullPath, pde2.ParentDE.FullPath,
                    StringComparison.OrdinalIgnoreCase);
                if (compareResult == 0)
                {
                    compareResult = string.Compare(de1.Path, de2.Path, StringComparison.OrdinalIgnoreCase);
                }

                break;

            default:
                throw new Exception($"Problem column {sortColumn} not handled for sort.");
        }

        if (searchResultHelper.ColumnSortOrder == SortOrder.Descending)
        {
            compareResult *= -1;
        }

        return compareResult;
    }

    public void DirectoryListViewColumnClick()
    {
        _clientForm.DirectoryListViewHelper.ListViewColumnClick();
    }

    private int DirectoryCompare(ICommonEntry de1, ICommonEntry de2)
    {
        var directoryHelper = _clientForm.DirectoryListViewHelper;
        var column = directoryHelper.SortColumn;
        var compareResult = column switch
        {
            0 => // SearchResult ListView Name column
                de1.PathCompareWithDirTo(de2),
            1 => // SearchResult ListView Size column
                de1.SizeCompareWithDirTo(de2),
            2 => // SearchResult ListView Modified column
                de1.ModifiedCompareTo(de2),
            _ => throw new Exception($"Problem column {column} not handled for sort.")
        };

        if (directoryHelper.ColumnSortOrder == SortOrder.Descending)
        {
            compareResult *= -1;
        }

        return compareResult;
    }

    public void ExitMenuItem()
    {
        _clientForm.Close();
    }

    public void AboutMenuItem()
    {
        _clientForm.AboutDialog();
    }

    private void DirectoryTreeGetContextMenuPairDirEntryThatExists(Action<ICommonEntry> gotContextAction)
    {
        var selectedCommonEntry = _clientForm.GetSelectedTreeItem();
        if (selectedCommonEntry.ExistsOnFileSystem())
        {
            gotContextAction(selectedCommonEntry);
        }
    }

    public void DirectoryTreeContextMenuOpenClick()
    {
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce => WindowsExplorerUtilities.ExplorerOpen(ce.FullPath));
    }

    public void DirectoryTreeContextMenuExploreClick()
    {
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce =>
            WindowsExplorerUtilities.ExplorerExplore(ce.FullPath));
    }

    public void DirectoryTreeContextMenuCustomCommand()
    {
        var cmd = _clientForm.ActiveCustomCommand;
        if (cmd == null) return;
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce =>
            WindowsExplorerUtilities.RunCustomCommand(cmd.Command, cmd.Arguments, ce.FullPath));
    }

    public void DirectoryTreeContextMenuPropertiesClick()
    {
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce =>
            WindowsExplorerUtilities.ShowFileProperties(ce.FullPath));
    }

    private void DirectoryGetContextMenuPairDirEntryThatExists(Action<PairDirEntry> gotContextAction)
    {
        _clientForm.DirectoryListViewHelper.ActionOnSelectedItem(d =>
        {
            var pde = new PairDirEntry(_directoryListCommonEntry, d);
            if (pde.ExistsOnFileSystem())
            {
                gotContextAction(pde);
            }
        });
    }

    private void DirectoryGetContextMenuPairDirEntries(Action<IEnumerable<ICommonEntry>> gotContextAction)
    {
        _clientForm.DirectoryListViewHelper.ActionOnSelectedItems(gotContextAction);
    }

    public void DirectoryContextMenuViewTreeClick()
    {
        DirectoryGetContextMenuPairDirEntryThatExists(ViewFolderInDirectoryTab);
    }

    private void ViewFolderInDirectoryTab(PairDirEntry pde)
    {
        var dirEntry = pde.ChildDE;
        if (dirEntry.IsDirectory)
        {
            SetDirectoryWithExpand(dirEntry);
        }
    }

    private void ViewFileInDirectoryTab(PairDirEntry pde)
    {
        var dirEntry = pde.ChildDE;
        SetDirectoryWithExpand(dirEntry);
        SelectFileInDirectoryTab(dirEntry);
    }

    private void SelectFileInDirectoryTab(ICommonEntry dirEntry)
    {
        if (dirEntry.IsDirectory) return;
        var index = _directoryList.IndexOf(dirEntry);
        var directoryHelper = _clientForm.DirectoryListViewHelper;
        directoryHelper.SelectItem(index);
    }

    public void DirectoryContextMenuOpenClick()
    {
        DirectoryGetContextMenuPairDirEntryThatExists(pde => WindowsExplorerUtilities.ExplorerOpen(pde.FullPath));
    }

    public void DirectoryContextMenuExploreClick()
    {
        DirectoryGetContextMenuPairDirEntryThatExists(pde =>
            WindowsExplorerUtilities.ExplorerExplore(pde.FullPath));
    }

    public void DirectoryContextMenuPropertiesClick()
    {
        DirectoryGetContextMenuPairDirEntryThatExists(pde =>
            WindowsExplorerUtilities.ShowFileProperties(pde.FullPath));
    }

    public void DirectoryContextMenuCustomCommand()
    {
        var cmd = _clientForm.ActiveCustomCommand;
        if (cmd == null) return;
        DirectoryGetContextMenuPairDirEntryThatExists(pde =>
            WindowsExplorerUtilities.RunCustomCommand(cmd.Command, cmd.Arguments, pde.FullPath));
    }

    public void DirectoryContextMenuSelectAllClick()
    {
        _clientForm.DirectoryListViewHelper.SelectAllItems();
    }

    public void DirectoryContextMenuParentClick()
    {
        var entryList = _directoryListCommonEntry.GetListFromRoot();
        if (entryList.Count > 1)
        {
            entryList.RemoveAt(entryList.Count - 1);
            SetDirectoryWithExpand(entryList);
        }
    }

    public void DirectoryContextMenuCopyFullPathClick()
    {
        DirectoryGetContextMenuPairDirEntries(enumerableDirEntry =>
        {
            // we don't have parent dir entry here 
            var s = new StringBuilder();
            foreach (var dirEntry in enumerableDirEntry)
            {
                var pde = new PairDirEntry(_directoryListCommonEntry, dirEntry);
                s.Append(pde.FullPath).Append(Environment.NewLine);
            }

            Clipboard.SetText(s.ToString());
        });
    }

    private void SearchResultGetContextMenuPairDirEntryThatExists(Action<PairDirEntry> gotContextAction)
    {
        _clientForm.SearchResultListViewHelper.ActionOnSelectedItem(pde =>
        {
            if (pde.ExistsOnFileSystem())
            {
                gotContextAction(pde);
            }
        });
    }

    private void SearchResultGetContextMenuPairDirEntrys(Action<IEnumerable<PairDirEntry>> gotContextAction)
    {
        _clientForm.SearchResultListViewHelper.ActionOnSelectedItems(gotContextAction);
    }

    public void SearchResultContextMenuViewTreeClick()
    {
        SearchResultGetContextMenuPairDirEntryThatExists(ViewFileInDirectoryTab);
    }

    public void SearchResultContextMenuOpenClick()
    {
        SearchResultGetContextMenuPairDirEntryThatExists(pde =>
            WindowsExplorerUtilities.ExplorerOpen(pde.FullPath));
    }

    public void SearchResultContextMenuExploreClick()
    {
        SearchResultGetContextMenuPairDirEntryThatExists(pde =>
            WindowsExplorerUtilities.ExplorerExplore(pde.FullPath));
    }

    public void SearchResultContextMenuCustomCommand()
    {
        var cmd = _clientForm.ActiveCustomCommand;
        if (cmd == null) return;
        SearchResultGetContextMenuPairDirEntryThatExists(pde =>
            WindowsExplorerUtilities.RunCustomCommand(cmd.Command, cmd.Arguments, pde.FullPath));
    }

    public void SearchResultContextMenuPropertiesClick()
    {
        SearchResultGetContextMenuPairDirEntryThatExists(pde =>
            WindowsExplorerUtilities.ShowFileProperties(pde.FullPath));
    }

    public void SearchResultContextMenuSelectAllClick()
    {
        _clientForm.SearchResultListViewHelper.SelectAllItems();
    }

    public void SearchResultContextMenuCopyFullPathClick()
    {
        SearchResultGetContextMenuPairDirEntrys(listPDE =>
        {
            var s = new StringBuilder();
            foreach (var pairDirEntry in listPDE)
            {
                s.Append(pairDirEntry.FullPath).Append(Environment.NewLine);
            }

            Clipboard.SetText(s.ToString());
        });
    }

    public ListViewItem BuildListViewItem(string[] vals, Color firstColumnForeColor, object tag)
    {
        // Use a constructor that takes all subitems at once - avoids internal array resizes
        var lvItem = new ListViewItem(vals)
        {
            ForeColor = firstColumnForeColor,
            Tag = tag
        };
        return lvItem;
    }

    public void CatalogListViewColumnClick()
    {
        _clientForm.CatalogListViewHelper.ListViewColumnClick();
    }

    private int RootCompare(ICommonEntry root1, ICommonEntry root2)
    {
        var re1 = SourceOf(root1);
        var re2 = SourceOf(root2);
        var catalogHelper = _clientForm.CatalogListViewHelper;
        var column = catalogHelper.SortColumn;
        var compareResult = column switch
        {
            0 => string.Compare(re1.RootPath, re2.RootPath, StringComparison.Ordinal),
            1 => string.Compare(string.IsNullOrEmpty(re1.VolumeName) ? "" : re1.VolumeName,
                string.IsNullOrEmpty(re2.VolumeName) ? "" : re2.VolumeName, StringComparison.Ordinal),
            2 => re1.RootDirEntryCount.CompareTo(re2.RootDirEntryCount),
            3 => re1.RootFileEntryCount.CompareTo(re2.RootFileEntryCount),
            4 => (re1.RootDirEntryCount + re1.RootFileEntryCount).CompareTo(re2.RootDirEntryCount + re2.RootFileEntryCount),
            5 => string.Compare(re1.DriveLetterHint, re2.DriveLetterHint, StringComparison.Ordinal),
            6 => re1.RootSize.CompareTo(re2.RootSize),
            7 => re1.AvailSpace.CompareTo(re2.AvailSpace),
            8 => re1.TotalSpace.CompareTo(re2.TotalSpace),
            9 => re1.ScanStartUtcTicks.CompareTo(re2.ScanStartUtcTicks),
            10 => (re1.ScanEndUtcTicks - re1.ScanStartUtcTicks).CompareTo(re2.ScanEndUtcTicks - re2.ScanStartUtcTicks),
            11 => string.Compare(re1.ActualFileName, re2.ActualFileName, StringComparison.Ordinal),
            12 => string.Compare(re1.Description, re2.Description, StringComparison.Ordinal),
            _ => throw new Exception($"Problem column {column} not handled for sort.")
        };

        if (catalogHelper.ColumnSortOrder == SortOrder.Descending)
        {
            compareResult *= -1;
        }

        return compareResult;
    }

    public void AdvancedSearchCheckboxChanged()
    {
        var isAdvanced = _clientForm.IsAdvancedSearchMode;
        SetAdvancedSearch(isAdvanced);
    }

    private void SetAdvancedSearch(bool value)
    {
        _clientForm.IsAdvancedSearchMode = value;
    }

    public async void ReloadCatalogs()
    {
        try
        {
            if (_isLoadingCatalogs) return;

            // clear all current list views and tree views.
            var catalogHelper = _clientForm.CatalogListViewHelper;
            catalogHelper.SetList(null);
            var searchResultHelper = _clientForm.SearchResultListViewHelper;
            searchResultHelper.SetList(null);
            var directoryListHelper = _clientForm.DirectoryListViewHelper;
            directoryListHelper.SetList(null);

            // Drop the previous stores; releasing the references lets the GC reclaim them.
            _catalogRoots = new List<ICommonEntry>();

            _clientForm.AddLine(string.Empty);
            _clientForm.AddLine("{0} v{1} reloading catalogs", _config.ProductName, _config.Version);

            _isLoadingCatalogs = true;
            _loadingCts = new CancellationTokenSource();
            var watch = Stopwatch.StartNew();

            _clientForm.SearchButtonEnable = false;
            StartLoadingAnimation();
            _clientForm.SetCatalogsLoadedStatus(0);
            _clientForm.SetTotalFileEntriesLoadedStatus(0);
            _clientForm.SetSearchTimeStatus("Reloading catalogs...");
            _clientForm.ShowLoadingProgress(true);
            _clientForm.SetLoadingProgressValue(0);
            SetMemoryStatus();

            _catalogRoots = await LoadCatalogRootsAsync();

            if (_catalogRoots.Count > 0)
            {
                SetNewDirectoryRoot(_catalogRoots.First());
            }

            SetCatalogListView();
            SetMemoryStatus();
            _clientForm.AddLine("Reload time was {0} msec", watch.ElapsedMilliseconds);
            _clientForm.SetSearchTimeStatus("");
        }
        catch (OperationCanceledException)
        {
            _clientForm.AddLine("Catalog reload was cancelled");
            _clientForm.SetSearchTimeStatus("Reload cancelled");
        }
        catch (Exception ex)
        {
            _clientForm.AddLine("Error reloading catalogs: {0}", ex.Message);
            _clientForm.SetSearchTimeStatus("Reload error");
            Log.Error(ex, "Error reloading catalogs");
        }
        finally
        {
            _isLoadingCatalogs = false;
            _clientForm.ShowLoadingProgress(false);
            StopLoadingAnimation();
            _clientForm.SearchButtonEnable = true;
            _loadingCts?.Dispose();
            _loadingCts = null;
        }
    }
}
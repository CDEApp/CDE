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
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Formatting;
using cdeAppCore.Search;
using cdeAppCore.Session;
using cdeAppCore.Shell;
using cdeAppCore.Sorting;
using cdeAppCore.Validation;
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

    // The catalog session owns the loaded catalogs (zero-copy mmap .cdex readers or in-memory stores),
    // their lifetime, and load/dispose. Roots are still exposed as ICommonEntry (EntryRef) so the
    // existing WinForms virtual views and tree work unchanged in-process.
    private readonly ICatalogSession _session;
    private readonly ISearchService _searchService;
    private readonly IShellActions _shellActions;
    private readonly IConfig _config;

    private static IEntrySource SourceOf(ICommonEntry root) => ((EntryRef)root).Source;

    // Catalog of a search-result pair (its entries are EntryRefs into a source).
    private static IEntrySource SourceOfPair(PairDirEntry pde) => (pde.ChildDE as EntryRef)?.Source;

    private static IShellActions CreateDefaultShellActions()
        => OperatingSystem.IsWindows()
            ? new WindowsShellActions([], Log.Logger)
            : new NoopShellActions([], Log.Logger);

    private readonly string[] _directoryVals;
    private readonly string[] _searchVals;
    private readonly string[] _catalogVals;

    // Pure size/date cell formatting (relocated to cdeAppCore), holding the configured date format
    // plus its bounded date cache.
    private readonly EntryFormatter _formatter;

    private List<PairDirEntry> _searchResultList;
    private List<ICommonEntry> _directoryList;

    /// <summary>
    /// The entry that is the parent of the Directory List View displayed items.
    /// </summary>
    private ICommonEntry _directoryListCommonEntry;

    private CancellationTokenSource _searchCts;
    private bool _isSearchButton;
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
        ILoadCatalogService loadCatalogService = null,
        IShellActions shellActions = null)
        : base(form)
    {
        _clientForm = form;
        _config = config;
        _session = new CatalogSession(loadCatalogService, Log.Logger);
        _searchService = new SearchService(_session);
        _shellActions = shellActions ?? CreateDefaultShellActions();
        _formatter = new EntryFormatter(_config.DateFormatYMDHMS);

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
            await _session.LoadAsync(_config.ConfigPath,
                new CallbackProgress<CatalogLoadProgress>(p => OnLoadProgress(p.Current, p.Total, p.Message)),
                _loadingCts.Token);

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
        var count = catalogHelper.SetList(_session.Roots.ToList());
        catalogHelper.SortList();
        _clientForm.SetCatalogsLoadedStatus(count);
        _clientForm.SetTotalFileEntriesLoadedStatus((int)_session.TotalEntryCount);
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
        // Scan times are unique per catalog, so format live (no cache) exactly as before.
        vals[9] = string.Format(_config.DateFormatYMDHMS, scanStart.ToLocalTime());
        vals[10] = $"{TimeSpan.FromMilliseconds(scanDurationMs).TotalSeconds:0.} sec";
        vals[11] = s.ActualFileName;
        vals[12] = s.Description;

        return listViewForeColor;
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

        if (!ValidateSearchFilters())
        {
            // Reset button if validation fails
            SetSearchButton(true);
            return;
        }

        _clientForm.AddSearchTextBoxAutoComplete(_clientForm.Pattern);

        _ = RunSearchAsync(BuildSearchQuery());
    }

    private SearchQuery BuildSearchQuery() => new()
    {
        LimitResultCount = _clientForm.LimitResultHelper.SelectedValue,
        Pattern = OptimiseRegexPattern(_clientForm.Pattern),
        RegexMode = _clientForm.RegexMode,
        IncludePath = _clientForm.IncludePathInSearch,
        IncludeFiles = _clientForm.IncludeFiles,
        IncludeFolders = _clientForm.IncludeFolders,
        FromSizeEnable = _clientForm.FromSize.Checked, FromSize = FromSizeValue(),
        ToSizeEnable = _clientForm.ToSize.Checked, ToSize = ToSizeValue(),
        FromDateEnable = _clientForm.FromDate.Checked, FromDate = _clientForm.FromDateValue.Date,
        ToDateEnable = _clientForm.ToDate.Checked, ToDate = _clientForm.ToDateValue.Date,
        FromHourEnable = _clientForm.FromHour.Checked, FromHour = _clientForm.FromHourValue.TimeOfDay,
        ToHourEnable = _clientForm.ToHour.Checked, ToHour = _clientForm.ToHourValue.TimeOfDay,
        NotOlderThanEnable = _clientForm.NotOlderThan.Checked, NotOlderThan = NotOlderThanValue()
    };

    // Stream results from the shared search service, mapping each SearchResultRow back to a
    // PairDirEntry so the existing virtual result view / sort / context menus work unchanged. Live
    // updates and progress are throttled to ~100ms; cancellation is cooperative via the token.
    private async Task RunSearchAsync(SearchQuery query)
    {
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        var results = new List<PairDirEntry>(500);
        var timer = Stopwatch.StartNew();
        var lastRefresh = Stopwatch.GetTimestamp();
        var refreshTicks = Stopwatch.Frequency / 10; // ~100ms live updates

        // Progress<T> marshals the callback back to the UI thread (context captured at construction).
        var progress = new Progress<SearchProgress>(p =>
            _clientForm.SetSearchTimeStatus("% " + (p.Total > 0 ? (int)(100.0 * p.Count / p.Total) : 0)));

        try
        {
            await foreach (var row in _searchService.SearchAsync(query, progress, token))
            {
                results.Add(ToPairDirEntry(row));

                var now = Stopwatch.GetTimestamp();
                if (now - lastRefresh >= refreshTicks)
                {
                    lastRefresh = now;
                    _clientForm.SetSearchResultStatus(SetSearchResultList(results));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user — keep whatever was found so far.
        }
        catch (Exception ex)
        {
            _clientForm.MessageBox(ex.Message);
        }
        finally
        {
            timer.Stop();
            Log.Logger.Information(
                "Search execution time: {ExecutionTime} ms, Total found {TotalFound}",
                timer.ElapsedMilliseconds, results.Count);

            _clientForm.SetSearchResultStatus(SetSearchResultList(results));
            _clientForm.SearchResultListViewHelper.SortList();
            SetSearchButton(true);
            _searchCts?.Dispose();
            _searchCts = null;
        }
    }

    private PairDirEntry ToPairDirEntry(SearchResultRow row)
    {
        var source = _session.GetSource(row.Ref.CatalogId);
        var childIndex = row.Ref.EntryIndex;
        return new PairDirEntry(
            new EntryRef(source, source.ParentOf(childIndex)),
            new EntryRef(source, childIndex));
    }

    // Validate the search filters via the frontend-agnostic core validator; show the first failure
    // (regex, then size, date, hour) in a message box. Returns true when the query is valid.
    private bool ValidateSearchFilters()
    {
        var result = SearchFilterValidator.Validate(
            _clientForm.RegexMode, _clientForm.Pattern,
            _clientForm.FromSize.Checked, FromSizeValue(),
            _clientForm.ToSize.Checked, ToSizeValue(),
            _clientForm.FromDate.Checked, _clientForm.FromDateValue.Date,
            _clientForm.ToDate.Checked, _clientForm.ToDateValue.Date,
            _clientForm.FromHour.Checked, _clientForm.FromHourValue.TimeOfDay,
            _clientForm.ToHour.Checked, _clientForm.ToHourValue.TimeOfDay);

        if (result.IsValid) return true;
        _clientForm.MessageBox(result.Message);
        return false;
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

    protected int SetSearchResultList(List<PairDirEntry> list)
    {
        _searchResultList = list;
        return _clientForm.SearchResultListViewHelper.SetList(list);
    }

    private void CancelSearch()
    {
        _searchCts?.Cancel();
    }

    // Minimal IProgress<T> that invokes the callback synchronously on the reporting thread. The load
    // path's OnLoadProgress already marshals to the UI thread itself, preserving prior behaviour.
    private sealed class CallbackProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
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

    private Color CreateRowValuesForDirectory(IList<string> vals, ICommonEntry dirEntry, Color itemColor)
    {
        vals[0] = dirEntry.Path;
        vals[1] = EntryFormatter.FormatDirectorySizeCell(dirEntry);
        if (dirEntry.IsDirectory)
        {
            itemColor = _listViewDirForeColor;
        }

        vals[2] = _formatter.FormatModifiedCell(dirEntry);
        return itemColor;
    }

    // before form closes capture any changed configuration.
    public void MyFormClosing()
    {
        CancelLoading();
        _config.RecordConfig(_clientForm);
        _session.Dispose(); // close any memory-mapped .cdex catalogs
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
        var searchResultHelper = _clientForm.SearchResultListViewHelper;
        return EntrySortComparer.CompareSearchResult(pde1, pde2,
            searchResultHelper.SortColumn,
            searchResultHelper.ColumnSortOrder == SortOrder.Descending);
    }

    public void DirectoryListViewColumnClick()
    {
        _clientForm.DirectoryListViewHelper.ListViewColumnClick();
    }

    private int DirectoryCompare(ICommonEntry de1, ICommonEntry de2)
    {
        var directoryHelper = _clientForm.DirectoryListViewHelper;
        return EntrySortComparer.CompareDirectory(de1, de2,
            directoryHelper.SortColumn,
            directoryHelper.ColumnSortOrder == SortOrder.Descending);
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
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce => _shellActions.Open(ce.FullPath));
    }

    public void DirectoryTreeContextMenuExploreClick()
    {
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce =>
            _shellActions.Explore(ce.FullPath));
    }

    public void DirectoryTreeContextMenuCustomCommand()
    {
        var cmd = _clientForm.ActiveCustomCommand;
        if (cmd == null) return;
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce =>
            _shellActions.RunCustomCommand(cmd, ce.FullPath));
    }

    public void DirectoryTreeContextMenuPropertiesClick()
    {
        DirectoryTreeGetContextMenuPairDirEntryThatExists(ce =>
            _shellActions.ShowProperties(ce.FullPath));
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
        DirectoryGetContextMenuPairDirEntryThatExists(pde => _shellActions.Open(pde.FullPath));
    }

    public void DirectoryContextMenuExploreClick()
    {
        DirectoryGetContextMenuPairDirEntryThatExists(pde =>
            _shellActions.Explore(pde.FullPath));
    }

    public void DirectoryContextMenuPropertiesClick()
    {
        DirectoryGetContextMenuPairDirEntryThatExists(pde =>
            _shellActions.ShowProperties(pde.FullPath));
    }

    public void DirectoryContextMenuCustomCommand()
    {
        var cmd = _clientForm.ActiveCustomCommand;
        if (cmd == null) return;
        DirectoryGetContextMenuPairDirEntryThatExists(pde =>
            _shellActions.RunCustomCommand(cmd, pde.FullPath));
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
            _shellActions.Open(pde.FullPath));
    }

    public void SearchResultContextMenuExploreClick()
    {
        SearchResultGetContextMenuPairDirEntryThatExists(pde =>
            _shellActions.Explore(pde.FullPath));
    }

    public void SearchResultContextMenuCustomCommand()
    {
        var cmd = _clientForm.ActiveCustomCommand;
        if (cmd == null) return;
        SearchResultGetContextMenuPairDirEntryThatExists(pde =>
            _shellActions.RunCustomCommand(cmd, pde.FullPath));
    }

    public void SearchResultContextMenuPropertiesClick()
    {
        SearchResultGetContextMenuPairDirEntryThatExists(pde =>
            _shellActions.ShowProperties(pde.FullPath));
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
        var catalogHelper = _clientForm.CatalogListViewHelper;
        return EntrySortComparer.CompareCatalog(root1, root2,
            catalogHelper.SortColumn,
            catalogHelper.ColumnSortOrder == SortOrder.Descending);
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

            await _session.LoadAsync(_config.ConfigPath,
                new CallbackProgress<CatalogLoadProgress>(p => OnLoadProgress(p.Current, p.Total, p.Message)),
                _loadingCts.Token);

            if (_session.Roots.Count > 0)
            {
                SetNewDirectoryRoot(_session.Roots[0]);
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
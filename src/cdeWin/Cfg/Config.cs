using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ProtoBuf;

namespace cdeWin.Cfg;

public class Config : IConfig
{
    public string DateFormatYMDHMS => "{0:yyyy/MM/dd HH:mm:ss}";
    public string DateCustomFormatYMD => "yyyy/MM/dd";
    public string DateCustomFormatHMS => "HH:mm:ss";
    public string LinkRepository => "https://github.com/CDEApp/CDE";
        
    public string Version { get; }
    public string ProductName { get; }

    private const string CdeConfigPath = "cde";
    private string _configFileName;
    private string _configPath;
    private string _configFullFileName;

    private readonly Configuration _default = new()
    {
        MainWindowConfig = new WindowConfig
        {
            Left = -1,
            Top = -1,
            Width = 700,
            Height = 550
        },
        SearchResultListView = new ListViewConfig
        {
            Columns =
            [
                new() { Name = "Name", Width = 260 },
                new() { Name = "Size", Width = 90, Alignment = HorizontalAlignment.Right },
                new() { Name = "Modified", Width = 130 },
                new() { Name = "Catalog", Width = 130 },
                new() { Name = "Path", Width = 400 }
            ]
        },
        DirectoryListView = new ListViewConfig
        {
            Columns =
            [
                new() { Name = "Name", Width = 260 },
                new() { Name = "Size", Width = 90, Alignment = HorizontalAlignment.Right },
                new() { Name = "Modified", Width = 130 }
            ]
        },
        CatalogListView = new ListViewConfig
        {
            Columns =
            [
                new() { Name = "Root Path", Width = 100 },
                new() { Name = "Volume Name", Width = 100 },
                new() { Name = "Dirs", Width = 60, Alignment = HorizontalAlignment.Right },
                new() { Name = "Files", Width = 60, Alignment = HorizontalAlignment.Right },
                new() { Name = "Dir+File", Width = 60, Alignment = HorizontalAlignment.Right },
                new() { Name = "Drive Hint", Width = 60 },
                new() { Name = "Used", Width = 70, Alignment = HorizontalAlignment.Right },
                new() { Name = "Available", Width = 70, Alignment = HorizontalAlignment.Right },
                new() { Name = "Size", Width = 70, Alignment = HorizontalAlignment.Right },
                new() { Name = "Created", Width = 130 },
                new() { Name = "Scan Time", Width = 70, Alignment = HorizontalAlignment.Right },
                new() { Name = "Catalog File", Width = 150 },
                new() { Name = "Description", Width = 150 }
            ]
        },
        DirectoryPaneSplitterRatio = -1f,
        Pattern = string.Empty,
        RegexMode = false,
        IsAdvancedSearchMode = false,
        LimitResultCountIndex = -1, // the initial default value is set by win forms configuration code
        FromSizeDropDownIndex = -1, // the initial default value is set by win forms configuration code
        ToSizeDropDownIndex = -1, // the initial default value is set by win forms configuration code
        NotOlderThanDropDownIndex = -1, // the initial default value is set by win forms configuration code
        PatternHistoryMaximum = 50
    };

    public Configuration Active { get; set; }

    public Config(string configFileName, string productName, string version)
    {
        BuildConfigPath(configFileName);
        var loaded = Read(_configFullFileName);
        Active = loaded ?? _default;
        ProductName = productName;
        Version = version;
    }

    private void BuildConfigPath(string configFileName)
    {
        _configPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _configFileName = configFileName;
        _configPath = Path.Combine(_configPath, CdeConfigPath);
        Directory.CreateDirectory(_configPath);
        _configFullFileName = Path.Combine(_configPath, _configFileName);
    }

    public string ConfigPath => _configPath;

    private Configuration Read(string fileName)
    {
        if (File.Exists(fileName))
        {
            try
            {
                using var fs = File.Open(fileName, FileMode.Open);
                return Read(fs);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }
        }
        return null;
    }

    public bool Save()
    {
        return Save(_configFullFileName);
    }

    private bool Save(string fileName)
    {
        // we do want exception to percolate out if there is a problem.
        using var newFs = File.Open(fileName, FileMode.Create);
        Write(newFs);
        return true;
    }

    private static Configuration Read(Stream input)
    {
        return Serializer.Deserialize<Configuration>(input);
    }

    private void Write(Stream output)
    {
        Serializer.Serialize(output, Active);
    }

    public void RecordConfig(ICDEWinForm form)
    {
        // Record values before the Form is closed.
        Active.DirectoryListView.RecordColumnWidths(form.DirectoryListViewHelper.ColumnConfigs());
        Active.SearchResultListView.RecordColumnWidths(form.SearchResultListViewHelper.ColumnConfigs());
        Active.CatalogListView.RecordColumnWidths(form.CatalogListViewHelper.ColumnConfigs());
        Active.PreviousPatternHistory = form.GetSearchTextBoxAutoComplete();
        Active.DirectoryPaneSplitterRatio = form.DirectoryPanelSplitterRatio;
        Active.Pattern = form.Pattern;
        Active.RegexMode = form.RegexMode;
        Active.IncludePath = form.IncludePathInSearch;
        Active.FindEntryFilter = form.FindEntryFilter;
        Active.IsAdvancedSearchMode = form.IsAdvancedSearchMode;
        Active.LimitResultCountIndex = form.LimitResultHelper.SelectedIndex;
        Active.FromSizeDropDownIndex = form.FromSizeDropDownHelper.SelectedIndex;
        Active.ToSizeDropDownIndex = form.ToSizeDropDownHelper.SelectedIndex;
        Active.NotOlderThanDropDownIndex = form.NotOlderThanDropDownHelper.SelectedIndex;
        Active.FromSizeField = form.FromSizeValue.Field;
        Active.ToSizeField = form.ToSizeValue.Field;
        Active.NotOlderThanField = form.NotOlderThanValue.Field;
        Active.FromDateValue = form.FromDateValue;
        Active.ToDateValue = form.ToDateValue;
        Active.FromHourValue = form.FromHourValue;
        Active.ToHourValue = form.ToHourValue;
    }

    public void RestoreConfigFormTopLeft(Form form)
    {
        Active.MainWindowConfig.RestoreFormTopLeft(form);
    }

    public void RestoreConfigFormBase(Form form)
    {
        Active.MainWindowConfig.RestoreForm(form);
    }

    public void RestoreConfig(ICDEWinForm form)
    {
        form.DirectoryListViewHelper.SetColumnConfigs(
            RestoreColumnConfig(_default.DirectoryListView, Active.DirectoryListView));
        form.SearchResultListViewHelper.SetColumnConfigs(
            RestoreColumnConfig(_default.SearchResultListView, Active.SearchResultListView));
        form.CatalogListViewHelper.SetColumnConfigs(
            RestoreColumnConfig(_default.CatalogListView, Active.CatalogListView));

        form.SetSearchTextBoxAutoComplete(Active.PreviousPatternHistory);
        form.DirectoryPanelSplitterRatio = Active.DirectoryPaneSplitterRatio;
        form.Pattern = Active.Pattern;
        form.RegexMode = Active.RegexMode;
        form.IncludePathInSearch = Active.IncludePath;
        form.FindEntryFilter = Active.FindEntryFilter;
        form.IsAdvancedSearchMode = Active.IsAdvancedSearchMode;
        form.LimitResultHelper.SelectedIndex = Active.LimitResultCountIndex;
        form.FromSizeDropDownHelper.SelectedIndex = Active.FromSizeDropDownIndex;
        form.ToSizeDropDownHelper.SelectedIndex = Active.ToSizeDropDownIndex;
        form.NotOlderThanDropDownHelper.SelectedIndex = Active.NotOlderThanDropDownIndex;
        form.FromSizeValue.Field = Active.FromSizeField;
        form.ToSizeValue.Field = Active.ToSizeField;
        form.NotOlderThanValue.Field = Active.NotOlderThanField;

        form.FromDateValue = DateOrNow(Active.FromDateValue);
        form.ToDateValue = DateOrNow(Active.ToDateValue);
        form.FromHourValue = DateOrNow(Active.FromHourValue);
        form.ToHourValue = DateOrNow(Active.ToHourValue);

        if (Active.PatternHistoryMaximum <= 0)
        {
            Active.PatternHistoryMaximum = _default.PatternHistoryMaximum;
        }
    }

    private static DateTime DateOrNow(DateTime dateValue)
    {
        if (dateValue <= DateTime.MinValue
            || dateValue >= DateTime.MaxValue)
        {
            return DateTime.Now;
        }
        return dateValue;
    }

    private static IEnumerable<ColumnConfig> RestoreColumnConfig(ListViewConfig initial, ListViewConfig active)
    {
        var columnMismatch = false;
        if (initial.Columns.Count == active.Columns.Count)
        {
            var comparer = new StrictKeyEqualityComparer<ColumnConfig, string>(x => x.Name);
            columnMismatch = !initial.Columns.SequenceEqual(active.Columns, comparer);
        }

        // if column count mismatch or column mismatch, replace columns with initial.
        if (initial.Columns.Count != active.Columns.Count
            || columnMismatch)
        {
            active.Columns = initial.Columns.ToList(); // copy initial columns to active.
        }
        return active.Columns;
    }

    // improve test easy on CDEWinFormPresenter.
    public int DefaultSearchResultColumnCount => Active != null ? ColumnCount(_default.SearchResultListView) : 0;

    // improve test easy on CDEWinFormPresenter.
    public int DefaultDirectoryColumnCount => Active != null ? ColumnCount(_default.DirectoryListView) : 0;

    // improve test easy on CDEWinFormPresenter.
    public int DefaultCatalogColumnCount => Active != null ? ColumnCount(_default.CatalogListView) : 0;

    private static int ColumnCount(ListViewConfig lvc)
    {
        return lvc?.Columns?.Count ?? 0;
    }

    public int CompareWithInfo(string s1, string s2)
    {
        return string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
    }
}
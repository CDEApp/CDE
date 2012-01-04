using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ProtoBuf;

namespace cdeWin
{
    [ProtoContract]
    public class WindowConfig
    {
        /// <summary>
        /// -1 means dont set this value. 
        /// </summary>
        [ProtoMember(1)]
        public int Left;

        /// <summary>
        /// -1 means dont set this value. 
        /// </summary>
        [ProtoMember(2)]
        public int Top;

        [ProtoMember(3)]
        public int Width;

        [ProtoMember(4)]
        public int Height;

        [ProtoMember(5)]
        public FormWindowState WindowState;

        public void RestoreForm(Form form)
        {
            var left = Left;
            var top = Top;
            var restoreRect = new Rectangle(left, top, Width, Height);

            if (Left != -1 && Top != -1 && restoreRect.IsVisibleOnAnyScreen())
            {   // Position sanity check before we allow manual restore.
                form.StartPosition = FormStartPosition.Manual;
            }

            form.WindowState = FormWindowState.Normal;
            form.DesktopBounds = restoreRect;
            form.WindowState = WindowState;
        }

        public void RecordForm(Form form)
        {
            WindowState = form.WindowState;

            var effectiveBounds = form.DesktopBounds;
            if (WindowState != FormWindowState.Normal)
            {
                effectiveBounds = form.RestoreBounds;
            }

            Left = effectiveBounds.X;
            Top = effectiveBounds.Y;
            Width = effectiveBounds.Width;
            Height = effectiveBounds.Height;
        }

    }

    [ProtoContract]
    public class ColumnConfig
    {
        [ProtoMember(1)]
        public int Width;
        [ProtoMember(2)]
        public string Name;
        [ProtoMember(3)]
        [DefaultValue(HorizontalAlignment.Left)]
        public HorizontalAlignment Alignment = HorizontalAlignment.Left;

        // columns can not currently be hidden.
        // columss can not currently be reordered.
    }

    [ProtoContract]
    public class ListViewConfig
    {
        [ProtoMember(1)]
        public List<ColumnConfig> Columns;

        /// <summary>
        /// Only capture column width changes, nothing else at the moment.
        /// </summary>
        public void RecordColumnWidths(IEnumerable<ColumnConfig> liveColumns)
        {
            foreach (var liveColumn in liveColumns)
            {
                var saveCol = Columns.FirstOrDefault(x => x.Name == liveColumn.Name);
                if (saveCol != null)
                {
                    saveCol.Width = liveColumn.Width;
                }
            } 
        }
    }

    [ProtoContract]
    public class Configuration
    {
        public const string Magic = "cdeWinViewConfig";
        [ProtoMember(1)]
        public string MagicVersion = Magic + "0001";
        [ProtoMember(2)]
        public WindowConfig MainWindowConfig;
        [ProtoMember(3)]
        public ListViewConfig SearchResultListView;
        [ProtoMember(4)]
        public ListViewConfig DirectoryListView;
        [ProtoMember(5)]
        public ListViewConfig CatalogListView;

        public const int DefaultPatternHistoryLength = 20;
        [ProtoMember(6)]
        public int PatternHistoryLength = DefaultPatternHistoryLength;
        [ProtoMember(7)]
        public List<string> PreviousPatternHistory;
        [ProtoMember(8)]
        public float DirectoryPaneSplitterRatio;
        [ProtoMember(9)]
        public string Pattern;
        [ProtoMember(10)]
        public bool RegexMode;
        [ProtoMember(11)]
        public bool IncludePath;
        [ProtoMember(12)]
        public int FindEntryFilter;
        [ProtoMember(13)]
        public bool IsAdvancedSearchMode;
        [ProtoMember(14)]
        public int LimitResultCountIndex;
        [ProtoMember(15)]
        public int FromSizeDropDownIndex;
        [ProtoMember(16)]
        public int ToSizeDropDownIndex;
        [ProtoMember(17)]
        public int NotOlderThanDropDownIndex;
        [ProtoMember(18)]
        public decimal FromSizeField;
        [ProtoMember(19)]
        public decimal ToSizeField;
        [ProtoMember(20)]
        public decimal NotOlderThanField;
        [ProtoMember(21)]
        public DateTime FromDateValue;
        [ProtoMember(22)]
        public DateTime ToDateValue;
        [ProtoMember(23)]
        public DateTime FromHourValue;
        [ProtoMember(24)]
        public DateTime ToHourValue;

        public Configuration()
        {
            PreviousPatternHistory = new List<string>(DefaultPatternHistoryLength);
        }

    }

    public interface IConfig
    {
        Configuration Active { get; set; }
        void RecordConfig(ICDEWinForm form);
        int SearchResultColumnCount { get; }
        int DirectoryColumnCount { get; }
        int CatalogColumnCount { get; }
    }

    public class Config : IConfig
    {
        // ReSharper disable InconsistentNaming
        public const string DateFormatYMDHMS = "{0:yyyy/MM/dd HH:mm:ss}";
        public const string DateCustomFormatYMD = "yyyy/MM/dd";
        public const string DateCustomFormatHMS = "HH:mm:ss";
        // ReSharper restore InconsistentNaming

        public const CompareOptions MyCompareOptions = CompareOptions.IgnoreCase | CompareOptions.StringSort;
        public static readonly CompareInfo MyCompareInfo = CompareInfo.GetCompareInfo("en-US");

        private readonly string _configSubPath = "cde";
        private string _configFileName;
        private string _configPath;
        private string _configFullFileName;

        public Configuration Default = new Configuration
        {
            MainWindowConfig = new WindowConfig
            {
                Left = -1,
                Top = -1,
                Width=700,
                Height=550
            },
            SearchResultListView = new ListViewConfig
            {
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name="Name", Width=260},
                    new ColumnConfig { Name="Size", Width=90, Alignment = HorizontalAlignment.Right },
                    new ColumnConfig { Name="Modified", Width=130},
                    new ColumnConfig { Name="Path", Width=400},
                }
            },
            DirectoryListView = new ListViewConfig
            {
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name="Name", Width=260},
                    new ColumnConfig { Name="Size", Width=90, Alignment = HorizontalAlignment.Right },
                    new ColumnConfig { Name="Modified", Width=130 },
                }
            },
            CatalogListView = new ListViewConfig
            {
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { Name="Root Path", Width=100},
                    new ColumnConfig { Name="Volume Name", Width=100},
                    new ColumnConfig { Name="Dirs", Width=60, Alignment = HorizontalAlignment.Right },
                    new ColumnConfig { Name="Files", Width=60, Alignment = HorizontalAlignment.Right },
                    new ColumnConfig { Name="D+F", Width=60, Alignment = HorizontalAlignment.Right },
                    new ColumnConfig { Name="Drive Hint", Width=60},
                    new ColumnConfig { Name="Space", Width=70, Alignment = HorizontalAlignment.Right },
                    new ColumnConfig { Name="Used", Width=70, Alignment = HorizontalAlignment.Right },
                    new ColumnConfig { Name="Created", Width=130 }, // NOTE convert from UTC ?
                    new ColumnConfig { Name="Catalog File", Width=150 },
                    new ColumnConfig { Name="Description", Width=150 },
                }
            },
            DirectoryPaneSplitterRatio = -1f,
            Pattern = string.Empty,
            RegexMode = false,
            IsAdvancedSearchMode = false,
            LimitResultCountIndex = -1, // initial default value is set by win forms configuratoin code
            FromSizeDropDownIndex = -1, // initial default value is set by win forms configuratoin code
            ToSizeDropDownIndex = -1, // initial default value is set by win forms configuratoin code
            NotOlderThanDropDownIndex = -1, // initial default value is set by win forms configuratoin code
        };

        public Configuration Loaded;

        public Configuration Active { get; set; }

        public Config(string configFileName)
        {
            BuildConfigPath(configFileName);
            Loaded = Read(_configFullFileName);
            Active = Loaded ?? Default;
        }

        private void BuildConfigPath(string configFileName)
        {
            _configPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _configFileName = configFileName;
            var justPath = Path.Combine(_configPath, _configSubPath);
            Directory.CreateDirectory(justPath);
            _configFullFileName = Path.Combine(justPath, _configFileName);
        }

        private Configuration Read(string fileName)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    using (var fs = File.Open(fileName, FileMode.Open))
                    {
                        return Read(fs);
                    }
                }
                // ReSharper disable EmptyGeneralCatchClause
                catch { }
                // ReSharper restore EmptyGeneralCatchClause
            }
            return null;
        }

        public bool Save()
        {
            return Save(_configFullFileName);
        }

        public bool Save(string fileName)
        {
            try
            {
                using (var newFs = File.Open(fileName, FileMode.Create))
                {
                    Write(newFs);
                    return true;
                }
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch { }
            // ReSharper restore EmptyGeneralCatchClause
            return false;
        }

        private Configuration Read(Stream input)
        {
            return Serializer.Deserialize<Configuration>(input);
        }

        private void Write(Stream output)
        {
            Serializer.Serialize(output, Active);
        }

        public void RecordConfig(ICDEWinForm form)
        {
            // Record values before Form is closed.
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

        public void RestoreConfig(ICDEWinForm form)
        {
            form.DirectoryListViewHelper.SetColumnConfigs(Active.DirectoryListView.Columns);
            form.SearchResultListViewHelper.SetColumnConfigs(Active.SearchResultListView.Columns);
            form.CatalogListViewHelper.SetColumnConfigs(Active.CatalogListView.Columns);
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
            form.FromDateValue = Active.FromDateValue;
            form.ToDateValue = Active.ToDateValue;
            form.FromHourValue = Active.FromHourValue;
            form.ToHourValue = Active.ToHourValue;
        }

        // improve test easy on CDEWinFormPresenter.
        public int SearchResultColumnCount
        {
            get { return Active != null ? ColumnCount(Active.SearchResultListView) : 0; }
        }

        // improve test easy on CDEWinFormPresenter.
        public int DirectoryColumnCount
        {
            get { return Active != null ? ColumnCount(Active.DirectoryListView) : 0; }
        }

        // improve test easy on CDEWinFormPresenter.
        public int CatalogColumnCount
        {
            get { return Active != null ? ColumnCount(Active.CatalogListView) : 0; }
        }

        private static int ColumnCount(ListViewConfig lvc)
        {
            return lvc != null && lvc.Columns != null ? lvc.Columns.Count : 0;
        }
    }
}
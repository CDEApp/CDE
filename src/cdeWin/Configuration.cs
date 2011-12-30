using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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

        public Configuration()
        {
            PreviousPatternHistory = new List<string>(DefaultPatternHistoryLength);
        }
    }

    public class Config
    {
        private readonly string _configFileName;

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
            RegexMode = false
        };

        public Configuration Loaded;

        public Configuration Active;

        public Config(string configFileName)
        {
            _configFileName = configFileName;
            Loaded = Read(_configFileName);
            Active = Loaded ?? Default;
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
            return Save(_configFileName);
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
        }
    }
}
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

        public void FromForm(Form form)
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
        [ProtoMember(1)] public List<ColumnConfig> Columns;

        /// <summary>
        /// Only capture column width changes, nothing else at the moment.
        /// </summary>
        public void SaveColumnWidths(ListView.ColumnHeaderCollection liveColumns)
        {
            foreach (ColumnHeader liveColumn in liveColumns)
            {
                var saveCol = Columns.FirstOrDefault(x => x.Name == liveColumn.Text);
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

        public const int DefaultPatternHistoryLength = 20;
        [ProtoMember(5)]
        public int PatternHistoryLength = DefaultPatternHistoryLength;
        [ProtoMember(6)]
        public List<string> PreviousRegexPattern;
        [ProtoMember(7)]
        public List<string> PreviousPattern;

        public Configuration()
        {
            PreviousRegexPattern = new List<string>(DefaultPatternHistoryLength);
            PreviousPattern = new List<string>(DefaultPatternHistoryLength);
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
                catch { }
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
            catch { }
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

        public void CaptureConfig(IDisplayTreeFromRootForm form)
        {
            // these values are not available after Form closed.
            Active.DirectoryListView.SaveColumnWidths(form.GetDirectoryListViewColumns);
            Active.SearchResultListView.SaveColumnWidths(form.GetSearchResultListViewColumns);
        }

        public void SetForm(DisplayTreeFromRootFormForm mainForm)
        {
            Active.MainWindowConfig.RestoreForm(mainForm);
            mainForm.SetDirectoryColumnHeaders(Active.DirectoryListView.Columns);
            mainForm.SetSearchColumnHeaders(Active.SearchResultListView.Columns);
        }
    }
}
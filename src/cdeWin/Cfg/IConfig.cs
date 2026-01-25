using System.Windows.Forms;
using cdeLib;

namespace cdeWin.Cfg;

public interface IConfig : IConfigCdeLib
{
    // ReSharper disable once InconsistentNaming
    string DateFormatYMDHMS { get; }
    // ReSharper disable once InconsistentNaming
    string DateCustomFormatYMD { get; }
    // ReSharper disable once InconsistentNaming
    string DateCustomFormatHMS { get; }
    string LinkRepository { get; }
    Configuration Active { get; set; }
    void RecordConfig(ICDEWinForm form);
    int DefaultSearchResultColumnCount { get; }
    int DefaultDirectoryColumnCount { get; }
    int DefaultCatalogColumnCount { get; }

    string Version { get; }
    string ProductName { get; }
    void RestoreConfigFormTopLeft(Form form);
    void RestoreConfigFormBase(Form form);
    void RestoreConfig(ICDEWinForm form);

    string ConfigPath { get; }
}
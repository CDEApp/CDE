using System;
using System.Collections.Generic;
using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    static class Program
    {
        private static List<RootEntry> RootEntries;

        [STAThread]
        static void Main()
        {
            // TODO consider using (var config = new Config()) { } - with Save built in.
            var config = new Config("cdeWinView.cfg"); // pickup from current directory for now.

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            RootEntries = RootEntry.LoadMultiDirCache(new[] {".", config.ConfigPath});

            var mainForm = new CDEWinForm();
            var mainPresenter = new CDEWinFormPresenter(mainForm, RootEntries, config);
            config.Active.MainWindowConfig.RestoreForm(mainForm);
            config.RestoreConfig(mainForm); // after presenter is configured and wired up events.
            //mainPresenter.Display();
            Application.Run(mainForm); // Trying, as Application.DoEvents() is used elsewhere.

            config.Active.MainWindowConfig.RecordForm(mainForm);
            config.Save();
        }
    }
}

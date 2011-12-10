﻿using System;
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
            RootEntries = RootEntry.LoadCurrentDirCache();

            var mainForm = new CDEWinForm();
            config.RestoreConfig(mainForm);

            var mainPresenter = new CDEWinFormPresenter(mainForm, RootEntries, config);
            mainPresenter.Display();
            //Application.Run(new CDEWinFormPresenter());

            var active = config.Active;
            active.MainWindowConfig.RecordForm(mainForm);
            config.Save();
        }
    }
}

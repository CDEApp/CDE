using System;
using System.Collections.Generic;
using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    static class Program
    {
        private static List<RootEntry> RootEntries;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            RootEntries = RootEntry.LoadCurrentDirCache();
            var displayTreeFromRootPresenter = new DisplayTreeFromRootPresenter(new DisplayTreeFromRootFormForm(), RootEntries);
            displayTreeFromRootPresenter.Display();
            //Application.Run(new DisplayTreeFromRootForm());
        }
    }
}

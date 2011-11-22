using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var re = new RootEntry { RootPath = @"C:\Test1" }; // TODO ? trailing slash bad in tree view IMO!
            var fe1 = new DirEntry { Name = "fe1", Size = 23, Modified = new DateTime(2011, 03, 13, 22, 10, 5, DateTimeKind.Local) };
            var de2 = new DirEntry { Name = "de2", IsDirectory = true };
            var fe23 = new DirEntry { Name = "fe23", Size = 35, Modified = new DateTime(2011, 04, 14, 23, 11, 6, DateTimeKind.Local) };
            var de3 = new DirEntry { Name = "de3", IsDirectory = true };
            var de31 = new DirEntry { Name = "de31", IsDirectory = true };
            var de311 = new DirEntry { Name = "de311", IsDirectory = true };
            var de3111 = new DirEntry { Name = "de3111", IsDirectory = true };
            var de3112 = new DirEntry { Name = "de3112", IsDirectory = true };
            var de31121 = new DirEntry { Name = "de31121", IsDirectory = true };
            var de3113 = new DirEntry { Name = "de3113", IsDirectory = true };
            var de31111 = new DirEntry { Name = "de31111", IsDirectory = true };
            re.Children.Add(fe1);
            re.Children.Add(de2);
            re.Children.Add(de3);

            de2.Children.Add(fe23);

            de3.Children.Add(de31);
            de31.Children.Add(de311);
            de311.Children.Add(de3111);
            de311.Children.Add(de3112);
            de3112.Children.Add(de31121);
            de311.Children.Add(de3113);
            de3111.Children.Add(de31111);
            re.SetInMemoryFields();

            var displayTreeFromRootPresenter = 
                new DisplayTreeFromRootPresenter(new DisplayTreeFromRootFormForm(), re);
            displayTreeFromRootPresenter.Display();
            //Application.Run(new DisplayTreeFromRootForm());
        }
    }
}

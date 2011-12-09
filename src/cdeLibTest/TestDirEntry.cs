using System.Collections.Generic;
using cdeLib;

namespace cdeLibTest
{
    /// <summary>
    /// Setup DirEntry with Children initialized for testing.
    /// </summary>
    public class TestDirEntry : DirEntry
    {
        //public TestDirEntry(bool isDirectory)
        //{
        //    if (isDirectory)
        //    {
        //        IsDirectory = true;
        //        Children = new List<DirEntry>();
        //    }
        //}

        public TestDirEntry(bool isDirectory)
        {
            if (isDirectory)
            {
                IsDirectory = true;
                Children = new List<DirEntry>();
            }  
        }
    }
}
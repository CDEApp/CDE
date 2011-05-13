using Alphaleonis.Win32.Filesystem;

namespace cdeLib
{
    public class PairDirEntry
    {
        public CommonEntry ParentDE;
        public DirEntry ChildDE;

        public string FilePath
        {
            get { return CommonEntry.MakeFullPath(ParentDE, ChildDE); }
        }

        public PairDirEntry(CommonEntry parent, DirEntry child)
        {
            ParentDE = parent;
            ChildDE = child;
        }
    }
}
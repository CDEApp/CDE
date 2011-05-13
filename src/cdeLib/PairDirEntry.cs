using Alphaleonis.Win32.Filesystem;

namespace cdeLib
{
    public class PairDirEntry
    {
        public CommonEntry ParentDE;
        public DirEntry ChildDE;

        public string FilePath
        {
            get
            {
                var a = ParentDE.FullPath ?? "<<pnull>>";
                var b = ChildDE.Name ?? "<<dnull>>";
                return Path.Combine(a, b);
            }
        }

        public PairDirEntry(CommonEntry parent, DirEntry child)
        {
            ParentDE = parent;
            ChildDE = child;
        }
    }
}
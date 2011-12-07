namespace cdeLib
{
    public class PairDirEntry
    {
        public readonly CommonEntry ParentDE;
        public readonly DirEntry ChildDE;
        public readonly RootEntry RootDE;

        public string FullPath
        {
            get { return CommonEntry.MakeFullPath(ParentDE, ChildDE); }
        }

        public PairDirEntry(CommonEntry parent, DirEntry child, RootEntry root)
        {
            ParentDE = parent;
            ChildDE = child;
            RootDE = root;
        }
    }
}
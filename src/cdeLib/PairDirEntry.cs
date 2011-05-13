namespace cdeLib
{
    public class PairDirEntry
    {
        public readonly CommonEntry ParentDE;
        public readonly DirEntry ChildDE;

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
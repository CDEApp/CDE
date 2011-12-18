using Alphaleonis.Win32.Filesystem;

namespace cdeLib
{
    public class PairDirEntry
    {
        public readonly CommonEntry ParentDE;
        public readonly DirEntry ChildDE;

        public string FullPath
        {
            get { return CommonEntry.MakeFullPath(ParentDE, ChildDE); }
        }

        public PairDirEntry(CommonEntry parent, DirEntry child)
        {
            ParentDE = parent;
            ChildDE = child;
        }

        /// <summary>
        /// TODO add checks for root and volume name for now just use path ?
        /// </summary>
        /// <returns></returns>
        public bool ExistsOnFileSystem()
        {
            var path = FullPath;
            return ChildDE.IsDirectory 
                ? Directory.Exists(path) 
                : File.Exists(path);
        }
    }
}
using System.Diagnostics;
using Alphaleonis.Win32.Filesystem;

namespace cdeLib
{
    [DebuggerDisplay("Size = {ChildDE.Size}")]
    public class PairDirEntry
    {
        public readonly CommonEntry ParentDE;

        public readonly DirEntry ChildDE;

        /// <summary>
        /// true if path or parent path ends with bad characters for NTFS, like Space or Period
        /// </summary>
        public readonly bool PathProblem;

        public string FullPath => CommonEntry.MakeFullPath(ParentDE, ChildDE);

        public PairDirEntry(CommonEntry parent, DirEntry child)
        {
            ParentDE = parent;
            ChildDE = child;
            PathProblem = ParentDE.PathProblem || ChildDE.PathProblem;
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
using Alphaleonis.Win32.Filesystem;

namespace cdeLib
{

    /// <summary>
    /// DTO for duplication.
    /// </summary>
    //[Obsolete("DirEntry now has a FullPath field populated on load and scan.")]
    public class FlatDirEntryDTO
    {
        public FlatDirEntryDTO(string filePath, DirEntry dirEntry)
        {
            //TODO: Get Rid of FilePath its chewing up too much RAM.   (IE Get rid of FlatDirEntryDTO as well)
            FilePath = filePath;
            DirEntry = dirEntry;
        }

        public string FilePath { get; set; }
        public DirEntry DirEntry { get; set; }
    }

    public class FlatDirEntry2
    {
        public FlatDirEntry2(CommonEntry parentEntry, DirEntry dirEntry)
        {
            ParentDirEntry = dirEntry;
            DirEntry = dirEntry;
        }

        public string FilePath
        { 
            get
            {
                return Path.Combine(ParentDirEntry.FullPath, DirEntry.Name);
            }
        }

        //public string FilePath { get; set; }
        public DirEntry ParentDirEntry { get; set; }
        public DirEntry DirEntry { get; set; }
    }
}
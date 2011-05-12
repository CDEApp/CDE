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
            ParentDirEntry = parentEntry;
            DirEntry = dirEntry;
        }

        public string FilePath
        { 
            get
            {
                var a = ParentDirEntry.FullPath ?? "pnull";
                var b = DirEntry.Name ?? "dnull";
                return Path.Combine(a, b);
            }
        }

        //public string FilePath { get; set; }
        public CommonEntry ParentDirEntry { get; set; }
        public DirEntry DirEntry { get; set; }
    }
}
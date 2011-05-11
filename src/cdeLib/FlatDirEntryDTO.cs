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
}
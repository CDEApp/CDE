namespace cdeLib
{

    /// <summary>
    /// DTO for duplication.
    /// </summary>
    public class FlatDirEntryDTO
    {
        public FlatDirEntryDTO(string filePath, DirEntry dirEntry)
        {
            FilePath = filePath;
            DirEntry = dirEntry;
        }

        public string FilePath { get; set; }
        public DirEntry DirEntry { get; set; }
    }
}
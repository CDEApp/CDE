namespace cdeWin;

public class LoadingState
{
    public LoadingState(int fileCount, int totalFiles)
    {
        FileCount = fileCount;
        TotalFiles = totalFiles;
    }

    public int FileCount { get; set; }

    public int TotalFiles { get; set; }
}
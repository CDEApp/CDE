using MediatR;

namespace cdeLib.Catalog;

public class ScanProgressEvent : INotification
{
    public ScanProgressEvent(int scanCount, string currentFile)
    {
        ScanCount = scanCount;
        CurrentFile = currentFile;
    }

    public string CurrentFile { get; set; }

    public int ScanCount { get; set; }
}

public class ScanCompletedEvent : INotification
{

}
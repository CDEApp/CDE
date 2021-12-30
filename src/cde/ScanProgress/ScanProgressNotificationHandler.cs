using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using MediatR;

namespace cde.ScanProgress;

public class ScanProgressNotificationHandler : INotificationHandler<ScanProgressEvent>
{
    public async Task Handle(ScanProgressEvent notification, CancellationToken cancellationToken)
    {
        await Task.Yield();
        ScanProgressConsole.ScanCount = notification.ScanCount;
        ScanProgressConsole.CurrentFile = notification.CurrentFile;
    }
}
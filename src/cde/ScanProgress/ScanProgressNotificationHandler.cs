using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using JetBrains.Annotations;
using MediatR;

namespace cde.ScanProgress;

[UsedImplicitly]
public class ScanProgressNotificationHandler : INotificationHandler<ScanProgressEvent>
{
    public Task Handle(ScanProgressEvent notification, CancellationToken cancellationToken)
    {
        ScanProgressConsole.ScanCount = notification.ScanCount;
        ScanProgressConsole.CurrentFile = notification.CurrentFile;
        return Task.CompletedTask;
    }
}
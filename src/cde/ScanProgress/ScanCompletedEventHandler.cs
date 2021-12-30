using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using MediatR;

namespace cde.ScanProgress;

public class ScanCompletedEventHandler : INotificationHandler<ScanCompletedEvent>
{
    public async Task Handle(ScanCompletedEvent notification, CancellationToken cancellationToken)
    {
        ScanProgressConsole.ScanIsComplete = true;
        await Task.Yield();
    }
}
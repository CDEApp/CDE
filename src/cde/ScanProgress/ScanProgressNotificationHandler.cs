using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cde.ScanProgress;

[UsedImplicitly]
public class ScanProgressNotificationHandler : IConsumer<ScanProgressEvent>
{
    public Task OnHandle(ScanProgressEvent message, CancellationToken cancellationToken)
    {
        ScanProgressConsole.ScanCount = message.ScanCount;
        ScanProgressConsole.CurrentFile = message.CurrentFile;
        return Task.CompletedTask;
    }
}
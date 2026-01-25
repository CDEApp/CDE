using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cde.ScanProgress;

[UsedImplicitly]
public class ScanCompletedEventHandler : IConsumer<ScanCompletedEvent>
{
    public async Task OnHandle(ScanCompletedEvent message, CancellationToken cancellationToken)
    {
        ScanProgressConsole.ScanIsComplete = true;
        await Task.Yield();
    }
}
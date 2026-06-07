using System.Threading;
using System.Threading.Tasks;
using cdeLib.Hashing;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cde.HashProgress;

[UsedImplicitly]
public class HashCompletedEventHandler : IConsumer<HashCompletedEvent>
{
    public async Task OnHandle(HashCompletedEvent message, CancellationToken cancellationToken)
    {
        HashProgressConsole.HashIsComplete = true;
        await Task.Yield();
    }
}

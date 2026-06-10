using System.Threading;
using System.Threading.Tasks;
using cdeLib.Hashing;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cde.HashProgress;

[UsedImplicitly]
public class HashProgressNotificationHandler : IConsumer<HashProgressEvent>
{
    public Task OnHandle(HashProgressEvent message, CancellationToken cancellationToken)
    {
        HashProgressConsole.FilesProcessed = message.FilesProcessed;
        HashProgressConsole.FilesToHash = message.FilesToHash;
        HashProgressConsole.Phase = message.Phase;
        return Task.CompletedTask;
    }
}

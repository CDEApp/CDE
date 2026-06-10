using System.Threading;
using System.Threading.Tasks;
using cdeLib.Hashing;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cde.HashProgress;

[UsedImplicitly]
public class HashStatusMessageHandler : IConsumer<HashStatusMessageEvent>
{
    public Task OnHandle(HashStatusMessageEvent message, CancellationToken cancellationToken)
    {
        HashProgressConsole.EnqueueMessage(message.Message);
        return Task.CompletedTask;
    }
}

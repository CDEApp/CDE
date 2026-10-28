using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cde.HashProgress;

/// <summary>
/// cde-layer override of the hash handler. Mirrors <see cref="cdeLib.Hashing.HashCatalogCommandHandler"/>
/// but renders a live Spectre progress display by publishing progress events emitted from <see cref="Duplication"/>.
/// The cdeLib handler is excluded from message-bus auto-declaration so this one is used by the CLI.
/// </summary>
[UsedImplicitly]
public class HashCatalogCommandHandler : IRequestHandler<HashCatalogCommand>
{
    private readonly Duplication _duplication;
    private readonly Serilog.ILogger _logger;
    private readonly IApplicationDiagnostics _applicationDiagnostics;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IMessageBus _messageBus;

    public HashCatalogCommandHandler(Serilog.ILogger logger, IApplicationDiagnostics applicationDiagnostics,
        Duplication duplication, ICatalogRepository catalogRepository, IMessageBus messageBus)
    {
        _logger = logger;
        _applicationDiagnostics = applicationDiagnostics;
        _duplication = duplication;
        _catalogRepository = catalogRepository;
        _messageBus = messageBus;
    }

    public async Task OnHandle(HashCatalogCommand request, CancellationToken cancellationToken)
    {
        // Hash operates on the columnar .cdex catalogs straight from their memory-mapped readers (no
        // pointer tree, issue #40), then rewrites each catalog with its new hashes.
        var cdexFiles = HashCatalogTargets.Resolve(request.CatalogPath, _catalogRepository, out var problem);
        if (cdexFiles.Count == 0)
        {
            _logger.Warning("{Problem}", problem);
            return;
        }

        // The token is the Ctrl-C break signal. For hashing a break ends the current phase (handled inside Duplication),
        // then the hashes so far are saved, so nothing here may stop on it: not the work, the display, or the
        // in-process notifications. The token stays cancelled after the first break, so passing it on would fail them.
        var mainLoopTask = Task.Run(() => MainLoop(cdexFiles), CancellationToken.None);
        var console = new HashProgressConsole();
        console.Start(mainLoopTask);
        await mainLoopTask.ConfigureAwait(false);
    }

    private async Task MainLoop(IList<string> cdexFiles)
    {
        _logger.Information("Memory pre-hash: {MemoryAllocated}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        // Route hashing progress and status lines to the Spectre console via the message bus.
        _duplication.ProgressEvent = (processed, toHash, phase) =>
            FireAndForget.Observe(_messageBus.Publish(new HashProgressEvent(processed, toHash, phase), cancellationToken: CancellationToken.None), nameof(HashProgressEvent));
        _duplication.StatusMessageEvent = message =>
            FireAndForget.Observe(_messageBus.Publish(new HashStatusMessageEvent(message), cancellationToken: CancellationToken.None), nameof(HashStatusMessageEvent));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await ColumnarCatalogHashing.HashAndSave(_duplication, cdexFiles,
                file => HashProgressConsole.EnqueueMessage($"Saving catalog {file}")).ConfigureAwait(false);

            var ts = stopwatch.Elapsed;
            var elapsedTime = string.Create(CultureInfo.InvariantCulture,
                $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}");
            HashProgressConsole.EnqueueMessage(
                $"Hash Took {elapsedTime}, Memory: {_applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()}");
            _logger.Information("Hash Took {ElapsedTime}, Memory: {Memory}", elapsedTime,
                _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        }
        finally
        {
            await _messageBus.Publish(new HashCompletedEvent(), cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}

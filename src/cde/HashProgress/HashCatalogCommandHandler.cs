using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
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
        // Hash operates on the columnar .cdex catalogs. The hashing engine is tree-based, so each
        // catalog is reconstructed into a mutable tree, hashed, then written back as a fresh .cdex.
        var cdexFiles = _catalogRepository.GetColumnarFileList(["./"]);
        if (cdexFiles.Count == 0)
        {
            _logger.Warning("No .cdex catalogs found. Run 'cde migrate' to create them first.");
            return;
        }

        var mainLoopTask = Task.Run(() => MainLoop(cdexFiles, cancellationToken), cancellationToken);
        var console = new HashProgressConsole();
        console.Start(mainLoopTask, cancellationToken);
        await mainLoopTask.ConfigureAwait(false);
    }

    private async Task MainLoop(IList<string> cdexFiles, CancellationToken cancellationToken)
    {
        _logger.Information("Memory pre-catalog load: {MemoryAllocated}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var rootEntries = CatalogTreeBuilder.FromColumnarFiles(cdexFiles);
        _logger.Information("Memory post-catalog load: {MemoryAllocated}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());

        // Route hashing progress and status lines to the Spectre console via the message bus.
        _duplication.ProgressEvent = (processed, toHash, phase) =>
            _messageBus.Publish(new HashProgressEvent(processed, toHash, phase), cancellationToken: cancellationToken);
        _duplication.StatusMessageEvent = message =>
            _messageBus.Publish(new HashStatusMessageEvent(message), cancellationToken: cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _duplication.ApplyHash(rootEntries).ConfigureAwait(false);

            foreach (var rootEntry in rootEntries)
            {
                HashProgressConsole.EnqueueMessage($"Saving catalog {rootEntry.ActualFileName}");
                ColumnarFormat.Write(EntryStore.Build(rootEntry), rootEntry.ActualFileName);
            }

            var ts = stopwatch.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            HashProgressConsole.EnqueueMessage(
                $"Hash Took {elapsedTime}, Memory: {_applicationDiagnostics.GetMemoryAllocated().FormatAsBytes()}");
            _logger.Information("Hash Took {ElapsedTime}, Memory: {Memory}", elapsedTime,
                _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        }
        finally
        {
            await _messageBus.Publish(new HashCompletedEvent(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

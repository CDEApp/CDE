using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cdeLib.Hashing;

[UsedImplicitly]
public class HashCatalogCommandHandler : IRequestHandler<HashCatalogCommand>
{
    private readonly Duplication _duplication;
    private readonly Serilog.ILogger _logger;
    private readonly IApplicationDiagnostics _applicationDiagnostics;
    private readonly ICatalogRepository _catalogRepository;

    public HashCatalogCommandHandler(Serilog.ILogger logger, IApplicationDiagnostics applicationDiagnostics,
        Duplication duplication, ICatalogRepository catalogRepository)
    {
        _logger = logger;
        _applicationDiagnostics = applicationDiagnostics;
        _duplication = duplication;
        _catalogRepository = catalogRepository;
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

        _logger.Information("Memory pre-catalog load: {MemoryAllocated}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var rootEntries = CatalogTreeBuilder.FromColumnarFiles(cdexFiles);
        _logger.Information("Memory post-catalog load: {MemoryAllocated}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var stopwatch = Stopwatch.StartNew();
        await _duplication.ApplyHash(rootEntries).ConfigureAwait(false);

        foreach (var rootEntry in rootEntries)
        {
            _logger.Information("Saving catalog {Filename}", rootEntry.ActualFileName);
            ColumnarFormat.Write(EntryStore.Build(rootEntry), rootEntry.ActualFileName);
        }

        var ts = stopwatch.Elapsed;
        var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
        _logger.Information("Hash Took {ElapsedTime}, Memory: {Memory}", elapsedTime, _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
    }
}
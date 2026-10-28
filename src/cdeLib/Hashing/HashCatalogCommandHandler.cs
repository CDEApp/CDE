using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Duplicates;
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
        // Hash operates on the columnar .cdex catalogs straight from their memory-mapped readers (no
        // pointer tree, issue #40), then rewrites each catalog with its new hashes.
        var cdexFiles = _catalogRepository.GetColumnarFileList(["./"]);
        if (cdexFiles.Count == 0)
        {
            _logger.Warning("No .cdex catalogs found. Run 'cde migrate' to create them first.");
            return;
        }

        _logger.Information("Memory pre-hash: {MemoryAllocated}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var stopwatch = Stopwatch.StartNew();
        await ColumnarCatalogHashing.HashAndSave(_duplication, cdexFiles,
            file => _logger.Information("Saving catalog {Filename}", file)).ConfigureAwait(false);

        var ts = stopwatch.Elapsed;
        var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
        _logger.Information("Hash Took {ElapsedTime}, Memory: {Memory}", elapsedTime, _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
    }
}
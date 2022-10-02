using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Duplicates;
using cdeLib.Infrastructure;
using MediatR;
using Serilog;

namespace cdeLib.Hashing;

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

    public async Task<Unit> Handle(HashCatalogCommand request, CancellationToken cancellationToken)
    {
        _logger.Information("Memory pre-catalog load: {0}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var rootEntries = _catalogRepository.LoadCurrentDirCache();
        _logger.Information("Memory post-catalog load: {0}",
            _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        var stopwatch = Stopwatch.StartNew();
        await _duplication.ApplyHash(rootEntries).ConfigureAwait(false);

        foreach (var rootEntry in rootEntries)
        {
            _logger.Information("Saving Catalog {0}", rootEntry.DefaultFileName);
            await _catalogRepository.Save(rootEntry).ConfigureAwait(false);
        }

        var ts = stopwatch.Elapsed;
        var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
        _logger.Information("Hash Took {0}, Memory: {1}", elapsedTime, _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
        return Unit.Value;
    }
}
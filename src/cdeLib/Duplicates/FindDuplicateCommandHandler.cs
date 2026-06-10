using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Entities;
using JetBrains.Annotations;
using Serilog;
using SlimMessageBus;

namespace cdeLib.Duplicates;

[UsedImplicitly]
public class FindDuplicateCommandHandler : IRequestHandler<FindDuplicatesCommand>
{
    private readonly Duplication _duplication;
    private readonly ICatalogRepository _catalogRepository;
    private readonly ILogger _logger;

    public FindDuplicateCommandHandler(Duplication duplication, ICatalogRepository catalogRepository,
        ILogger logger)
    {
        _duplication = duplication;
        _catalogRepository = catalogRepository;
        _logger = logger;
    }

    public Task OnHandle(FindDuplicatesCommand request, CancellationToken cancellationToken)
    {
        // Dupes reads hashes from the columnar .cdex catalogs; reconstruct trees and reuse the
        // existing duplicate-detection engine.
        var cdexFiles = _catalogRepository.GetColumnarFileList(["./"]);
        if (cdexFiles.Count == 0)
        {
            _logger.Warning("No .cdex catalogs found. Run 'cde migrate' to create them first.");
            return Task.CompletedTask;
        }

        _duplication.FindDuplicates(CatalogTreeBuilder.FromColumnarFiles(cdexFiles));
        return Task.CompletedTask;
    }
}
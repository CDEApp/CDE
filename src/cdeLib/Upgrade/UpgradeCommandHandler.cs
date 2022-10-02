using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using JetBrains.Annotations;
using MediatR;
using Serilog;

namespace cdeLib.Upgrade;

[UsedImplicitly]
public class UpgradeCommandHandler : IRequestHandler<UpgradeCommand>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ILogger _logger;
    private readonly MapV3ToV4Catalog _mapV3ToV4Catalog = new ();

    public UpgradeCommandHandler(ICatalogRepository catalogRepository, ILogger logger)
    {
        _catalogRepository = catalogRepository;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpgradeCommand request, CancellationToken cancellationToken)
    {
        // load catalog
        _logger.Debug("Loading catalogs");
        foreach (var cat in cdeDataStructure3.Entities.RootEntry.LoadCurrentDirCache())
        {
            _logger.Debug("Upgrading Catalog {Name}", cat.ActualFileName);

            // map to new structure
            var newRootEntry = _mapV3ToV4Catalog.Map(cat);

            // save
            await _catalogRepository.Save(newRootEntry);
        }

        return await Unit.Task;
    }
}
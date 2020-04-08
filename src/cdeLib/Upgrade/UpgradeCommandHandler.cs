using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using MediatR;
using Serilog;

namespace cdeLib.Upgrade
{
    public class UpgradeCommandHandler : IRequestHandler<UpgradeCommand>
    {
        private readonly ICatalogRepository _catalogRepository;
        private readonly ILogger _logger;
        private readonly MapV3ToV4Catalog _mapV3ToV4Catalog = new MapV3ToV4Catalog();

        public UpgradeCommandHandler(ICatalogRepository catalogRepository, ILogger logger)
        {
            _catalogRepository = catalogRepository;
            _logger = logger;
        }

        public async Task<Unit> Handle(UpgradeCommand request, CancellationToken cancellationToken)
        {
            //load catalog
            _logger.Debug("Loading catalogs");
            var existingCatalogs = cdeDataStructure3.Entities.RootEntry.LoadCurrentDirCache();

            foreach (var cat in existingCatalogs)
            {
                _logger.Debug("Catalog {name}", cat.ActualFileName);
                //convert
                var newRootEntry = _mapV3ToV4Catalog.Map(cat);

                //save
                await _catalogRepository.Save(newRootEntry);
            }

            return await Unit.Task;
        }
    }
}
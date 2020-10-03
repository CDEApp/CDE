using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using MediatR;
using Serilog;

namespace cdeLib.Upgrade
{
    public class UpdateCommand : IRequest
    {
        public string FileName { get; set; }
        public string Description { get; set; }
    }

    public class UpdateCommandHandler : IRequestHandler<UpdateCommand>
    {
        private readonly ICatalogRepository _catalogRepository;
        private readonly ILogger _logger;

        public UpdateCommandHandler(ICatalogRepository catalogRepository, ILogger logger)
        {
            _catalogRepository = catalogRepository;
            _logger = logger;
        }

        public async Task<Unit> Handle(UpdateCommand request, CancellationToken cancellationToken)
        {
            var rootEntry = _catalogRepository.LoadDirCache(request.FileName);
            var isDirty = false;

            if (!string.IsNullOrEmpty(request.Description))
            {
                _logger.Information("updating catalog {FileName} with description {Desc}", request.FileName,
                    request.Description);
                rootEntry.Description = request.Description;
                isDirty = true;
            }

            if (isDirty)
                await _catalogRepository.Save(rootEntry);
            return await Unit.Task;
        }
    }
}
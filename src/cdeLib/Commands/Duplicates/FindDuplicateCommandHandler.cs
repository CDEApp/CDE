using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace cdeLib.Duplicates
{
    public class FindDuplicateCommandHandler : IRequestHandler<FindDuplicatesCommand>
    {
        private readonly Duplication _duplication;
        private ICatalogRepository _catalogRepository;

        public FindDuplicateCommandHandler(Duplication duplication, ICatalogRepository catalogRepository)
        {
            _duplication = duplication;
            _catalogRepository = catalogRepository;
        }

        public Task<Unit> Handle(FindDuplicatesCommand request, CancellationToken cancellationToken)
        {
            _duplication.FindDuplicates(_catalogRepository.LoadCurrentDirCache());
            return Unit.Task;
        }
    }
}
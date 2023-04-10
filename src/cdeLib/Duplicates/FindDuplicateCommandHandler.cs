using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using JetBrains.Annotations;
using MediatR;

namespace cdeLib.Duplicates;

[UsedImplicitly]
public class FindDuplicateCommandHandler : IRequestHandler<FindDuplicatesCommand>
{
    private readonly Duplication _duplication;
    private readonly ICatalogRepository _catalogRepository;

    public FindDuplicateCommandHandler(Duplication duplication, ICatalogRepository catalogRepository)
    {
        _duplication = duplication;
        _catalogRepository = catalogRepository;
    }

    public Task Handle(FindDuplicatesCommand request, CancellationToken cancellationToken)
    {
        _duplication.FindDuplicates(_catalogRepository.LoadCurrentDirCache());
        return Task.CompletedTask;
    }
}
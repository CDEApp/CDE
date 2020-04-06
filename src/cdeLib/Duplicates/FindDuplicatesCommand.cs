using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace cdeLib.Duplicates
{
    public sealed class FindDuplicatesCommand : IRequest
    {
    }

    public class FindDuplicateCommandHandler : IRequestHandler<FindDuplicatesCommand>
    {
        private readonly Duplication _duplication;

        public FindDuplicateCommandHandler(Duplication duplication)
        {
            _duplication = duplication;
        }

        public Task<Unit> Handle(FindDuplicatesCommand request, CancellationToken cancellationToken)
        {
            _duplication.FindDuplicates(RootEntry.LoadCurrentDirCache());
            return Unit.Task;
        }
    }
}
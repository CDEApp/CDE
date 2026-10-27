using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Entities.Columnar;
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
        // Dupes is read-only: work directly over the memory-mapped .cdex readers, no pointer tree.
        var cdexFiles = _catalogRepository.GetColumnarFileList(["./"]);
        if (cdexFiles.Count == 0)
        {
            _logger.Warning("No .cdex catalogs found. Run 'cde migrate' to create them first.");
            return Task.CompletedTask;
        }

        var readers = new List<ColumnarCatalogReader>(cdexFiles.Count);
        try
        {
            foreach (var file in cdexFiles)
                readers.Add(new ColumnarCatalogReader(file));
            _duplication.FindDuplicates(readers);
        }
        finally
        {
            foreach (var reader in readers)
                reader.Dispose();
        }

        return Task.CompletedTask;
    }
}
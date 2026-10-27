using System;
using System.Collections.Generic;
using System.IO;
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
            {
                try
                {
                    readers.Add(new ColumnarCatalogReader(file));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                               or ArgumentException)
                {
                    // One corrupt or locked catalog must not abort the whole command.
                    _logger.Warning("Skipping unreadable catalog {File}: {Message}", file, ex.Message);
                }
            }

            if (readers.Count == 0)
            {
                _logger.Warning("No readable .cdex catalogs found.");
                return Task.CompletedTask;
            }

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
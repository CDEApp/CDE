﻿using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Catalog;
using cdeLib.Infrastructure;
using MediatR;

namespace cdeLib.Hashing
{
    public class HashCatalogCommandHandler : IRequestHandler<HashCatalogCommand>
    {
        private readonly Duplication _duplication;
        private readonly ILogger _logger;
        private readonly IApplicationDiagnostics _applicationDiagnostics;
        private readonly ICatalogRepository _catalogRepository;

        public HashCatalogCommandHandler(ILogger logger, IApplicationDiagnostics applicationDiagnostics,
            Duplication duplication, ICatalogRepository catalogRepository)
        {
            _logger = logger;
            _applicationDiagnostics = applicationDiagnostics;
            _duplication = duplication;
            _catalogRepository = catalogRepository;
        }

        public async Task<Unit> Handle(HashCatalogCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInfo("Memory pre-catalog load: {0}",
                _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            var rootEntries = _catalogRepository.LoadCurrentDirCache();
            _logger.LogInfo("Memory post-catalog load: {0}",
                _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            var stopwatch = Stopwatch.StartNew();
            await _duplication.ApplyHash(rootEntries);

            foreach (var rootEntry in rootEntries)
            {
                _logger.LogDebug("Saving Catalog {0}", rootEntry.DefaultFileName);
                await _catalogRepository.Save(rootEntry);
            }

            var ts = stopwatch.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            _logger.LogInfo("Hash Took {0}, Memory: {1}", elapsedTime, _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            return Unit.Value;
        }
    }
}
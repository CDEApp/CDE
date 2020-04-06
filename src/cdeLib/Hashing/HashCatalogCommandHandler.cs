using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Infrastructure;
using MediatR;

namespace cdeLib.Hashing
{
    public class HashCatalogCommandHandler : IRequestHandler<HashCatalogCommand>
    {
        private readonly Duplication _duplication;
        private readonly ILogger _logger;
        private readonly IApplicationDiagnostics _applicationDiagnostics;

        public HashCatalogCommandHandler(ILogger logger, IApplicationDiagnostics applicationDiagnostics,
            Duplication duplication)
        {
            _logger = logger;
            _applicationDiagnostics = applicationDiagnostics;
            _duplication = duplication;
        }

        public async Task<Unit> Handle(HashCatalogCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInfo("Memory pre-catalog load: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            var rootEntries = RootEntry.LoadCurrentDirCache();
            _logger.LogInfo("Memory post-catalog load: {0}", _applicationDiagnostics.GetMemoryAllocated().FormatAsBytes());
            var sw = new Stopwatch();
            sw.Start();

            _duplication.ApplyHash(rootEntries);

            foreach (var rootEntry in rootEntries)
            {
                _logger.LogDebug("Saving {0}", rootEntry.DefaultFileName);
                rootEntry.SaveRootEntry();
            }

            sw.Stop();
            var ts = sw.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            _logger.LogInfo("Hash Took {0}",elapsedTime);
            return await Unit.Task;
        }
    }
}
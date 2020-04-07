using System;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Infrastructure.Config;
using MediatR;

namespace cdeLib.Cache
{
    public class CreateCacheCommandHandler : IRequestHandler<CreateCacheCommand>
    {
        private readonly IConfiguration _configuration;
        private readonly ICatalogRepository _catalogRepository;

        public CreateCacheCommandHandler(IConfiguration configuration, ICatalogRepository catalogRepository)
        {
            _configuration = configuration;
            _catalogRepository = catalogRepository;
        }

        public Task<Unit> Handle(CreateCacheCommand request, CancellationToken cancellationToken)
        {
            var re = new RootEntry(_configuration);
            try
            {
                re.SimpleScanCountEvent = ScanCountPrintDot;
                re.SimpleScanEndEvent = ScanEndOfEntries;
                re.ExceptionEvent = PrintExceptions;

                re.PopulateRoot(request.Path);
                if (Hack.BreakConsoleFlag)
                {
                    Console.WriteLine(" * Break key detected incomplete scan will not be saved.");
                    return Unit.Task;
                }

                var oldRoot = _catalogRepository.LoadDirCache(re.DefaultFileName);
                if (oldRoot != null)
                {
                    Console.WriteLine($"Found cache \"{re.DefaultFileName}\"");
                    Console.WriteLine("Updating hashes on new scan from found cache file.");
                    oldRoot.TraverseTreesCopyHash(re);
                }

                re.SortAllChildrenByPath();
                re.SaveRootEntry();
                var scanTimeSpan = (re.ScanEndUTC - re.ScanStartUTC);
                Console.WriteLine($"Scanned Path {re.Path}");
                Console.WriteLine($"Scan time {scanTimeSpan.TotalMilliseconds:0.00} msecs");
                Console.WriteLine($"Saved Scanned Path {re.DefaultFileName}");
                Console.WriteLine(
                    $"Files {re.FileEntryCount:0,0} Dirs {re.DirEntryCount:0,0} Total Size of Files {re.Size:0,0}");
            }
            catch (ArgumentException aex)
            {
                Console.WriteLine($"Error: {aex.Message}");
            }

            return Unit.Task;
        }

        private static void PrintExceptions(string path, Exception ex)
        {
            Console.WriteLine($"Exception {ex.GetType()}, Path \"{path}\"");
        }

        private static void ScanCountPrintDot()
        {
            Console.Write(".");
        }

        private static void ScanEndOfEntries()
        {
            Console.WriteLine(string.Empty);
        }
    }
}
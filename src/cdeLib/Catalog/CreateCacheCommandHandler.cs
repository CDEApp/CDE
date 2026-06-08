using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure.Config;
using JetBrains.Annotations;
using SlimMessageBus;

namespace cdeLib.Catalog;

[UsedImplicitly]
public class CreateCacheCommandHandler : IRequestHandler<CreateCacheCommand>
{
    private readonly IConfiguration _configuration;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IMessageBus _messageBus;

    public CreateCacheCommandHandler(IConfiguration configuration, ICatalogRepository catalogRepository,
        IMessageBus messageBus)
    {
        _configuration = configuration;
        _catalogRepository = catalogRepository;
        _messageBus = messageBus;
    }

    public async Task OnHandle(CreateCacheCommand request, CancellationToken cancellationToken)
    {
        var re = new RootEntry(_configuration);
        try
        {
            re.SimpleScanCountEvent = (count, currentFile) =>
                _messageBus.Publish(new ScanProgressEvent(count, currentFile), cancellationToken: cancellationToken);
            re.SimpleScanEndEvent = ScanEndOfEntries;
            re.ExceptionEvent = PrintExceptions;

            re.PopulateRoot(request.Path, request.FollowJunctions);
            if (Hack.BreakConsoleFlag)
            {
                Console.WriteLine(" * Break key detected incomplete scan will not be saved.");
                return;
            }

            // Catalogs are stored in the zero-copy columnar .cdex format. Reuse hashes from an existing
            // .cdex (reconstructed into a tree) when one is found for this scan path.
            var cdexName = Path.ChangeExtension(re.DefaultFileName, ".cdex");
            if (File.Exists(cdexName))
            {
                Console.WriteLine($"Found cache \"{cdexName}\"");
                Console.WriteLine("Updating hashes on new scan from found cache file.");
                RootEntry oldRoot;
                using (var reader = new ColumnarCatalogReader(cdexName))
                {
                    oldRoot = CatalogTreeBuilder.FromSource(reader);
                }
                oldRoot.TraverseTreesCopyHash(re);
            }

            re.SortAllChildrenByPath();
            re.SetSummaryFields();
            if (!string.IsNullOrEmpty(request.Description))
            {
                re.Description = request.Description;
            }

            re.ActualFileName = cdexName;
            await Task.Run(() => ColumnarFormat.Write(EntryStore.Build(re), cdexName), cancellationToken);
            var scanTimeSpan = re.ScanEndUtc - re.ScanStartUtc;
            Console.WriteLine($"Scanned path {re.Path}");
            Console.WriteLine($"Scan time {scanTimeSpan.TotalMilliseconds:0.00} msecs");
            Console.WriteLine($"Saved scanned path {cdexName}");
            Console.WriteLine(
                $"Files {re.FileEntryCount:0,0} Dirs {re.DirEntryCount:0,0} Total Size of Files {re.Size:0,0} bytes");
        }
        catch (ArgumentException aex)
        {
            Console.WriteLine($"Error: {aex.Message}");
        }
    }

    private void PrintExceptions(string path, Exception ex)
    {
        Console.WriteLine($"Exception {ex.GetType()}, Path \"{path}\"");
    }

    private void ScanEndOfEntries()
    {
        Console.WriteLine();
    }
}
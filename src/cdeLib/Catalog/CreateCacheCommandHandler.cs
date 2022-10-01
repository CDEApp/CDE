using System;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using JetBrains.Annotations;
using MediatR;

namespace cdeLib.Catalog;

[UsedImplicitly]
public class CreateCacheCommandHandler : IRequestHandler<CreateCacheCommand>
{
    private readonly IConfiguration _configuration;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IMediator _mediator;

    public CreateCacheCommandHandler(IConfiguration configuration, ICatalogRepository catalogRepository, IMediator mediator)
    {
        _configuration = configuration;
        _catalogRepository = catalogRepository;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(CreateCacheCommand request, CancellationToken cancellationToken)
    {
        var re = new RootEntry(_configuration);
        try
        {
            re.SimpleScanCountEvent = (int count, string currentFile) => _mediator.Publish(new ScanProgressEvent(count, currentFile), cancellationToken);
            re.SimpleScanEndEvent = ScanEndOfEntries;
            re.ExceptionEvent = PrintExceptions;

            re.PopulateRoot(request.Path);
            if (Hack.BreakConsoleFlag)
            {
                Console.WriteLine(" * Break key detected incomplete scan will not be saved.");
                return Unit.Value;
            }

            var oldRoot = _catalogRepository.LoadDirCache(re.DefaultFileName);
            if (oldRoot != null)
            {
                Console.WriteLine($"Found cache \"{re.DefaultFileName}\"");
                Console.WriteLine("Updating hashes on new scan from found cache file.");
                oldRoot.TraverseTreesCopyHash(re);
            }

            re.SortAllChildrenByPath();
            re.SetSummaryFields();
            if (!string.IsNullOrEmpty(request.Description))
            {
                re.Description = request.Description;
            }
            await _catalogRepository.Save(re);
            var scanTimeSpan = re.ScanEndUTC - re.ScanStartUTC;
            Console.WriteLine($"Scanned path {re.Path}");
            Console.WriteLine($"Scan time {scanTimeSpan.TotalMilliseconds:0.00} msecs");
            Console.WriteLine($"Saved scanned path {re.DefaultFileName}");
            Console.WriteLine(
                $"Files {re.FileEntryCount:0,0} Dirs {re.DirEntryCount:0,0} Total Size of Files {re.Size:0,0}");
        }
        catch (ArgumentException aex)
        {
            Console.WriteLine($"Error: {aex.Message}");
        }

        return Unit.Value;
    }

    private void PrintExceptions(string path, Exception ex)
    {
        Console.WriteLine($"Exception {ex.GetType()}, Path \"{path}\"");
    }

    private void ScanCountPrintDot()
    {
        Console.Write(".");
    }

    private void ScanEndOfEntries()
    {
        Console.WriteLine();
    }
}
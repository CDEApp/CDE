using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using Humanizer;
using JetBrains.Annotations;
using MediatR;
using Serilog;

namespace cde.ScanProgress;

[UsedImplicitly]
public class CreateCacheCommandHandler : IRequestHandler<CreateCacheCommand>
{
    private readonly IConfiguration _configuration;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IMediator _mediator;

    public CreateCacheCommandHandler(IConfiguration configuration, ICatalogRepository catalogRepository,
        IMediator mediator)
    {
        _configuration = configuration;
        _catalogRepository = catalogRepository;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(CreateCacheCommand request, CancellationToken cancellationToken)
    {
        var mainLoopTask = Task.Factory.StartNew(() => MainLoop(request, cancellationToken), cancellationToken);
        var console = new ScanProgressConsole();
        console.Start(mainLoopTask, cancellationToken);
        await mainLoopTask.ConfigureAwait(false);
        return Unit.Value;
    }

    private async Task MainLoop(CreateCacheCommand request, CancellationToken cancellationToken)
    {
        var re = new RootEntry(_configuration);
        try
        {
            re.SimpleScanCountEvent = (count, currentFile) =>
                _mediator.Publish(new ScanProgressEvent(count, currentFile), cancellationToken);
            re.SimpleScanEndEvent = () => _mediator.Publish(new ScanCompletedEvent(), cancellationToken);
            re.ExceptionEvent = PrintException;

            re.PopulateRoot(request.Path);
            if (Hack.BreakConsoleFlag)
            {
                Console.WriteLine(" * Break key detected incomplete scan will not be saved.");
            }

            var oldRoot = _catalogRepository.LoadDirCache(re.DefaultFileName);
            if (oldRoot != null)
            {
                Log.Information("Found cache \"{FileName}\"", re.DefaultFileName);
                Log.Information("Updating hashes for new scan from cache file");
                oldRoot.TraverseTreesCopyHash(re);
            }

            re.SortAllChildrenByPath();
            re.SetSummaryFields();
            if (!string.IsNullOrEmpty(request.Description))
            {
                re.Description = request.Description;
            }

            await _catalogRepository.Save(re).ConfigureAwait(false);
            Console.WriteLine();
            Log.Information("Scanned path {Path}", re.Path);
            Log.Information("Saved to {Path}", re.DefaultFileName);
            Log.Information(
                "Scanned Files {FileCount:0,0}, Dirs {DirCount:0,0}, Total size {Size:0,0}", re.FileEntryCount,
                re.DirEntryCount, re.Size.Bytes().Humanize(CultureInfo.CurrentCulture));
        }
        catch (ArgumentException ex)
        {
            Log.Error(ex, "Error: {ErrorMessage}", ex.Message);
        }
    }

    private void PrintException(string path, Exception ex)
    {
        Console.WriteLine($"Exception {ex.GetType()}, Path \"{path}\"");
    }
}
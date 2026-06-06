using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Infrastructure.Config;
using Humanizer;
using JetBrains.Annotations;
using SlimMessageBus;
using Serilog;

namespace cde.ScanProgress;

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
        var mainLoopTask = Task.Run(() => MainLoop(request, cancellationToken), cancellationToken);
        var console = new ScanProgressConsole();
        console.Start(mainLoopTask, cancellationToken);
        await mainLoopTask.ConfigureAwait(false);
    }

    private async Task MainLoop(CreateCacheCommand request, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var re = new RootEntry(_configuration);
        try
        {
            re.SimpleScanCountEvent = (count, currentFile) =>
                _messageBus.Publish(new ScanProgressEvent(count, currentFile), cancellationToken: cancellationToken);
            re.SimpleScanEndEvent = () => _messageBus.Publish(new ScanCompletedEvent(), cancellationToken: cancellationToken);
            re.ExceptionEvent = PrintException;

            re.PopulateRoot(request.Path);
            if (Hack.BreakConsoleFlag)
            {
                Console.WriteLine(" * Break key detected incomplete scan will not be saved.");
                return;
            }

            // Catalogs are stored in the zero-copy columnar .cdex format. Reuse hashes from an existing
            // .cdex (reconstructed into a tree) for this scan path when one is found.
            var cdexName = Path.ChangeExtension(re.DefaultFileName, ".cdex");
            if (File.Exists(cdexName))
            {
                Log.Information("Found cache \"{FileName}\", Updating hashes for new scan from cache file", cdexName);
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

            ScanProgressConsole.EnqueueMessage("Saving catalog...");
            re.ActualFileName = cdexName;
            await Task.Run(() => ColumnarFormat.Write(EntryStore.Build(re), cdexName), cancellationToken)
                .ConfigureAwait(false);
            ScanProgressConsole.EnqueueMessage($"Saved to {cdexName}");

            // Calculate and display final scan summary
            sw.Stop();
            var elapsedSec = sw.ElapsedMilliseconds / 1000.0;
            if (elapsedSec < 1) elapsedSec = 1;
            var totalCount = re.FileEntryCount + re.DirEntryCount;
            var scansPerSec = (long)(totalCount / elapsedSec);
            var defaultNumberFormat = new NumberFormatInfo();
            var scanCountText = totalCount.ToString("N0", defaultNumberFormat);
            var scansPerSecText = scansPerSec.ToString("N0", defaultNumberFormat);
            ScanProgressConsole.EnqueueMessage($"Total files scanned: {scanCountText}, Average: {scansPerSecText}/sec");

            Log.Information("Scanned path {Path}, Saved to {SavePath}", re.Path, cdexName);
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
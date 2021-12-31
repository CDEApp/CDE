﻿using System;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Catalog;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using JetBrains.Annotations;
using MediatR;
using Serilog;

namespace cde.ScanProgress
{
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
            var mainLoopTask  = Task.Factory.StartNew(() => MainLoop(request, cancellationToken));
            //var mainLoopTask = MainLoop(request, cancellationToken);
            var console = new ScanProgressConsole();
            console.Start(mainLoopTask, cancellationToken);
            await mainLoopTask.ConfigureAwait(false);
            return Unit.Value;
        }

        private async Task MainLoop(CreateCacheCommand request, CancellationToken cancellationToken)
        {
            //await Task.Yield(); //If this isn't here this exits early!?!
            var re = new RootEntry(_configuration);
            try
            {
                re.SimpleScanCountEvent = (int count, string currentFile) =>
                    _mediator.Publish(new ScanProgressEvent(count, currentFile), cancellationToken);
                re.SimpleScanEndEvent = () => _mediator.Publish(new ScanCompletedEvent(), cancellationToken);
                re.ExceptionEvent = PrintExceptions;

                re.PopulateRoot(request.Path);
                if (Hack.BreakConsoleFlag)
                {
                    Console.WriteLine(" * Break key detected incomplete scan will not be saved.");
                }

                var oldRoot = _catalogRepository.LoadDirCache(re.DefaultFileName);
                if (oldRoot != null)
                {
                    Log.Information($"Found cache \"{re.DefaultFileName}\"");
                    Log.Information("Updating hashes on new scan from cache file.");
                    oldRoot.TraverseTreesCopyHash(re);
                }

                re.SortAllChildrenByPath();
                re.SetSummaryFields();
                if (!string.IsNullOrEmpty(request.Description))
                {
                    re.Description = request.Description;
                }

                await _catalogRepository.Save(re).ConfigureAwait(false);
                var scanTimeSpan = re.ScanEndUTC - re.ScanStartUTC;
                Console.WriteLine();
                // Console.WriteLine("*X*");
                Log.Information("Scanned Path {Path}", re.Path);
                Log.Information("Scan time {ScanTime:0.00} msecs", scanTimeSpan.TotalMilliseconds);
                Log.Information("Saved scanned path {Path}", re.DefaultFileName);
                Log.Information(
                    "Files {FileCount:0,0} Dirs {DirCount:0,0} Total Size of Files {Size:0,0}", re.FileEntryCount, re.DirEntryCount, re.Size);
                // Console.WriteLine("***");
            }
            catch (ArgumentException ex)
            {
                Log.Error(ex, "Error: {ErrorMessage}", ex.Message);
            }
            //await Task.Yield(); //If this isn't here this exits early!?!
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
            Console.WriteLine(string.Empty);
        }
    }
}
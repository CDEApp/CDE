using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Entities.Soa;

namespace cdeAppCore.Search;

/// <summary>
/// Streamed search over the catalog session's <see cref="IEntrySource"/> list. Mirrors the WinForms
/// presenter's search loop (same per-source <c>Find</c>, same result limit, same ~100ms throttled
/// progress) but yields <see cref="SearchResultRow"/> DTOs through a channel so any frontend can
/// consume the results as an <see cref="IAsyncEnumerable{T}"/>. The catalog never serializes; only
/// the matched rows are materialized.
/// </summary>
public sealed class SearchService : ISearchService
{
    private const int ChannelCapacity = 1024;

    private readonly ICatalogSession _session;

    public SearchService(ICatalogSession session)
    {
        _session = session;
    }

    public async IAsyncEnumerable<SearchResultRow> SearchAsync(
        SearchQuery query,
        IProgress<SearchProgress> progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Bounded so a slow consumer holds the producer back instead of letting rows pile up.
        var channel = Channel.CreateBounded<SearchResultRow>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true
            });
        // Lets us release a producer blocked on a full channel if the consumer stops enumerating early.
        using var producerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // The per-source Find is synchronous and CPU-bound; run it off the consumer's thread and
        // bridge matches through the channel.
        var producer = Task.Run(() => Produce(query, channel.Writer, progress, producerCts.Token), CancellationToken.None);

        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var row))
                {
                    yield return row;
                }
            }
        }
        finally
        {
            // Surface any producer exception and ensure the background work has finished.
            producerCts.Cancel();
            await producer.ConfigureAwait(false);
        }
    }

    private void Produce(SearchQuery query, ChannelWriter<SearchResultRow> writer,
        IProgress<SearchProgress> progress, CancellationToken ct)
    {
        Exception error = null;
        try
        {
            RunFind(query, writer, progress, ct);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal stop, not an error.
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            writer.Complete(error);
        }
    }

    private void RunFind(SearchQuery query, ChannelWriter<SearchResultRow> writer,
        IProgress<SearchProgress> progress, CancellationToken ct)
    {
        var opts = ToFindOptions(query);
        var limit = query.LimitResultCount;

        var grandTotal = 0;
        for (var i = 0; i < _session.CatalogCount; i++)
        {
            grandTotal += _session.GetSource(i).Count;
        }

        var matched = 0;
        var scannedBase = 0;
        var lastReport = Stopwatch.GetTimestamp();
        var reportTicks = Stopwatch.Frequency / 10; // ~100ms streaming, matching the WinForms search
        var started = Stopwatch.GetTimestamp();

        for (var catalogId = 0; catalogId < _session.CatalogCount; catalogId++)
        {
            if (ct.IsCancellationRequested || matched >= limit) break;

            var source = _session.GetSource(catalogId);
            var baseScanned = scannedBase;
            var localCatalogId = catalogId;

            source.Find(opts,
                onMatch: idx =>
                {
                    if (matched >= limit) return;
                    if (WriteBlocking(writer, BuildRow(source, localCatalogId, idx), ct)) matched++;
                },
                isCancelled: () => ct.IsCancellationRequested || matched >= limit,
                onScan: scanned => Report(baseScanned + scanned));

            scannedBase += source.Count;
        }

        // Final 100% progress report.
        progress?.Report(new SearchProgress(grandTotal, grandTotal, Stopwatch.GetElapsedTime(started)));
        return;

        void Report(int scanned)
        {
            if (progress == null) return;
            var now = Stopwatch.GetTimestamp();
            if (now - lastReport < reportTicks) return;
            lastReport = now;
            progress.Report(new SearchProgress(scanned, grandTotal, Stopwatch.GetElapsedTime(started)));
        }
    }

    // The find loop is synchronous CPU-bound work inside Task.Run, so waiting for space here is the
    // backpressure; it blocks that pool thread by design. TryWrite on a full bounded channel would
    // silently drop the row. Returns false (row not written) if the channel was completed.
    private static bool WriteBlocking(ChannelWriter<SearchResultRow> writer, SearchResultRow row, CancellationToken ct)
    {
        while (!writer.TryWrite(row))
        {
            // Throws OperationCanceledException when cancelled; Produce treats that as a normal stop.
            if (!writer.WaitToWriteAsync(ct).AsTask().GetAwaiter().GetResult()) return false;
        }

        return true;
    }

    private static SearchResultRow BuildRow(IEntrySource source, int catalogId, int idx)
    {
        // One parent-chain walk: build the parent path once, then derive the entry's full path from it
        // plus the name (same separator rule as EntryRef.MakeFullPath and the catalog path builders).
        var name = source.FullName(idx);
        var parentPath = source.FullPath(source.ParentOf(idx));
        var flags = source.FlagsOf(idx);
        return new SearchResultRow
        {
            Ref = new EntryRefDto(catalogId, idx),
            Name = name,
            Size = source.SizeOf(idx),
            Modified = source.ModifiedOf(idx),
            IsModifiedBad = (flags & Flags.ModifiedBad) == Flags.ModifiedBad,
            IsDirectory = source.IsDirectory(idx),
            IsReparsePoint = (flags & Flags.ReparsePoint) == Flags.ReparsePoint,
            FullPath = PathJoin.Child(parentPath, name),
            ParentPath = parentPath,
            CatalogName = source.DefaultFileName
        };
    }

    private static EntryStoreFindOptions ToFindOptions(SearchQuery q) => new()
    {
        Pattern = q.Pattern,
        RegexMode = q.RegexMode,
        IncludePath = q.IncludePath,
        IncludeFiles = q.IncludeFiles,
        IncludeFolders = q.IncludeFolders,
        FromSizeEnable = q.FromSizeEnable, FromSize = q.FromSize,
        ToSizeEnable = q.ToSizeEnable, ToSize = q.ToSize,
        FromDateEnable = q.FromDateEnable, FromDate = q.FromDate,
        ToDateEnable = q.ToDateEnable, ToDate = q.ToDate,
        FromHourEnable = q.FromHourEnable, FromHour = q.FromHour,
        ToHourEnable = q.ToHourEnable, ToHour = q.ToHour,
        NotOlderThanEnable = q.NotOlderThanEnable, NotOlderThan = q.NotOlderThan
    };
}

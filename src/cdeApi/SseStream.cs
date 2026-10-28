using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeLib;

namespace cdeApi;

/// <summary>The bounded hand-off between the producers (search / load) and the single SSE drain loop.</summary>
public static class SseQueue
{
    // Small on purpose: this only smooths bursts. When it is full the producer waits for the client.
    public const int DefaultCapacity = 1024;

    public static Channel<object> Create(int capacity = DefaultCapacity) => Channel.CreateBounded<object>(
        new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    /// <summary>Write one queued item as its SSE event. Items are boxed DTOs; progress is infrequent.</summary>
    public static void WriteItem(SseWriter w, object item)
    {
        switch (item)
        {
            case SearchResultRow r: w.Write("result"u8, r, AppCoreJsonContext.Default.SearchResultRow); break;
            case SearchProgress p: w.Write("progress"u8, p, AppCoreJsonContext.Default.SearchProgress); break;
            case CatalogLoadProgress l: w.Write("progress"u8, l, AppCoreJsonContext.Default.CatalogLoadProgress); break;
            case RescanProgress rp: w.Write("progress"u8, rp, AppCoreJsonContext.Default.RescanProgress); break;
            case RescanError re: w.Write("error"u8, re, AppCoreJsonContext.Default.RescanError); break;
            case CreateProgress cp: w.Write("progress"u8, cp, AppCoreJsonContext.Default.CreateProgress); break;
            case CreateCatalogError ce: w.Write("error"u8, ce, AppCoreJsonContext.Default.CreateCatalogError); break;
            case MigrateProgress mp: w.Write("progress"u8, mp, AppCoreJsonContext.Default.MigrateProgress); break;
            case MigrateError me: w.Write("error"u8, me, AppCoreJsonContext.Default.MigrateError); break;
            default: throw new InvalidOperationException($"No SSE mapping for queued item {item?.GetType()}");
        }
    }
}

/// <summary>
/// Bridges <see cref="IProgress{T}"/> reports into the SSE queue without ever blocking the producer. If the queue
/// is full the report is not queued but remembered; the newest such report is handed back by
/// <see cref="TakePending"/> so the final progress (e.g. 100%) still reaches the client before <c>done</c>.
/// </summary>
public sealed class SseProgress(ChannelWriter<object> writer)
    : IProgress<SearchProgress>, IProgress<CatalogLoadProgress>, IProgress<RescanProgress>, IProgress<CreateProgress>,
        IProgress<MigrateProgress>
{
    private object _pending;

    public void Report(SearchProgress value) => Offer(value);
    public void Report(CatalogLoadProgress value) => Offer(value);
    public void Report(RescanProgress value) => Offer(value);
    public void Report(CreateProgress value) => Offer(value);
    public void Report(MigrateProgress value) => Offer(value);

    private void Offer(object value)
    {
        // A queued report supersedes any older remembered one; a rejected one replaces it.
        Volatile.Write(ref _pending, writer.TryWrite(value) ? null : value);
    }

    /// <summary>The newest report that could not be queued, or null. Call only after the producer has finished.</summary>
    public object TakePending() => Interlocked.Exchange(ref _pending, null);
}

/// <summary>Runs one SSE stream: producer to bounded queue to <see cref="SseWriter"/>, then the final <c>done</c> event.</summary>
public static class SseStream
{
    /// <param name="pipe">The response body.</param>
    /// <param name="produce">Feeds rows into the writer and reports progress; honours the token.</param>
    /// <param name="writeDone">Writes the terminal <c>done</c> event, given the number of result events sent.</param>
    /// <param name="ct">Client-disconnect token (RequestAborted).</param>
    public static async Task RunAsync(PipeWriter pipe,
        Func<SseProgress, ChannelWriter<object>, CancellationToken, Task> produce,
        Action<SseWriter, int> writeDone, CancellationToken ct,
        int capacity = SseQueue.DefaultCapacity, int flushEvery = SseWriter.DefaultFlushEvery)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var channel = SseQueue.Create(capacity);
        var progress = new SseProgress(channel.Writer);

        var work = Task.Run(async () =>
        {
            try { await produce(progress, channel.Writer, cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* client disconnect / cancel */ }
            finally { channel.Writer.TryComplete(); }
        }, CancellationToken.None);

        try
        {
            using var sse = new SseWriter(pipe);
            var count = 0;
            var completed = await sse.DrainAsync(channel.Reader, (w, item) =>
            {
                if (item is SearchResultRow) count++;
                SseQueue.WriteItem(w, item);
            }, ct, flushEvery).ConfigureAwait(false);

            if (completed)
            {
                // Progress dropped because the queue was full: the newest still goes out ahead of done.
                if (progress.TakePending() is { } last) SseQueue.WriteItem(sse, last);
                writeDone(sse, count);
                await sse.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        finally
        {
            // Always observe the producer before cts is disposed; cancelling first releases a producer that is
            // waiting on a full queue if the drain stopped early.
            await cts.CancelAsync().ConfigureAwait(false);
            try { await work.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* normal stop */ }
        }
    }
}

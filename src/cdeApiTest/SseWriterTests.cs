using System;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Serialization;
using cdeLib;
using NUnit.Framework;

namespace cdeApiTest;

[TestFixture]
public class SseWriterTests
{
    private static SearchResultRow Row(string name, string path) => new()
    {
        Ref = new EntryRefDto(1, 42),
        Name = name,
        Size = 1234,
        Modified = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
        FullPath = path,
        ParentPath = @"C:\a",
        CatalogName = "C-test.cdex"
    };

    // The pre-change wire format, reproduced verbatim from the old Sse.EventAsync + JsonSerializer.Serialize.
    private static string OldFormat(string evt, string json) => $"event: {evt}\ndata: {json}\n\n";

    private static (SseWriter w, TestPipeWriter pipe) Create()
    {
        var pipe = new TestPipeWriter();
        return (new SseWriter(pipe), pipe);
    }

    [Test]
    public void Result_event_matches_known_good_literal_bytes()
    {
        var (w, pipe) = Create();
        w.Write("result"u8, Row("a.txt", @"C:\a\a.txt"), AppCoreJsonContext.Default.SearchResultRow);
        w.FlushAsync().AsTask().GetAwaiter().GetResult();
        Assert.That(pipe.Text, Is.EqualTo(
            "event: result\ndata: {\"ref\":{\"catalogId\":1,\"entryIndex\":42},\"name\":\"a.txt\",\"size\":1234," +
            "\"modified\":\"2024-05-06T07:08:09Z\",\"isModifiedBad\":false,\"isDirectory\":false," +
            "\"isReparsePoint\":false,\"fullPath\":\"C:\\\\a\\\\a.txt\",\"parentPath\":\"C:\\\\a\"," +
            "\"catalogName\":\"C-test.cdex\"}\n\n"));
    }

    [TestCase("plain.txt", @"C:\a\plain.txt")]
    [TestCase("caf\u00e9 \u65e5\u672c\u8a9e \U0001F600.txt", @"C:\a\caf\u00e9 \u65e5\u672c\u8a9e.txt")]
    [TestCase("quo\"te<>&'.txt", "C:\\a\\\"quoted\"\\x\ty")]
    public void Result_event_is_identical_to_the_old_string_format(string name, string path)
    {
        var row = Row(name, path);
        var (w, pipe) = Create();
        w.Write("result"u8, row, AppCoreJsonContext.Default.SearchResultRow);
        w.FlushAsync().AsTask().GetAwaiter().GetResult();

        var expected = OldFormat("result", JsonSerializer.Serialize(row, AppCoreJsonContext.Default.SearchResultRow));
        Assert.That(pipe.Text, Is.EqualTo(expected));
        Assert.That(pipe.Text.IndexOf('\n'), Is.EqualTo("event: result".Length), "data must be single line");
    }

    [Test]
    public void Progress_events_are_identical_to_the_old_string_format()
    {
        var sp = new SearchProgress(5, 10, TimeSpan.FromMilliseconds(1500));
        var lp = new CatalogLoadProgress(2, 3, "loading \"x\"\u00e9");
        var (w, pipe) = Create();
        w.Write("progress"u8, sp, AppCoreJsonContext.Default.SearchProgress);
        w.Write("progress"u8, lp, AppCoreJsonContext.Default.CatalogLoadProgress);
        w.FlushAsync().AsTask().GetAwaiter().GetResult();

        var expected = OldFormat("progress", JsonSerializer.Serialize(sp, AppCoreJsonContext.Default.SearchProgress))
                       + OldFormat("progress", JsonSerializer.Serialize(lp, AppCoreJsonContext.Default.CatalogLoadProgress));
        Assert.That(pipe.Text, Is.EqualTo(expected));
    }

    [Test]
    public void Done_events_match_the_old_anonymous_object_format()
    {
        var (w, pipe) = Create();
        w.WriteDone(7);
        w.WriteDone(3, 123456789012L);
        w.FlushAsync().AsTask().GetAwaiter().GetResult();
        Assert.That(pipe.Text, Is.EqualTo(
            OldFormat("done", JsonSerializer.Serialize(new { count = 7 })) +
            OldFormat("done", JsonSerializer.Serialize(new { catalogs = 3, entries = 123456789012L }))));
        Assert.That(pipe.Text, Is.EqualTo(
            "event: done\ndata: {\"count\":7}\n\nevent: done\ndata: {\"catalogs\":3,\"entries\":123456789012}\n\n"));
    }

    [Test]
    public async Task Drain_writes_everything_in_order_and_flushes_the_tail()
    {
        var (w, pipe) = Create();
        var ch = Channel.CreateUnbounded<object>();
        for (var i = 0; i < 150; i++) ch.Writer.TryWrite(Row("n" + i, @"C:\x"));
        ch.Writer.Complete();

        await w.DrainAsync(ch.Reader, Write, CancellationToken.None, flushEvery: 64);

        Assert.That(pipe.FlushedBytes, Is.EqualTo(pipe.Length), "nothing may be left unflushed");
        Assert.That(pipe.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(150));
        Assert.That(pipe.Text, Does.StartWith("event: result\ndata: {\"ref\":{\"catalogId\":1,\"entryIndex\":42},\"name\":\"n0\""));
        Assert.That(pipe.FlushCount, Is.LessThan(150), "must batch, not flush per event");
    }

    [Test]
    public async Task Drain_flushes_when_idle_so_a_lone_event_is_not_held_back()
    {
        var (w, pipe) = Create();
        var ch = Channel.CreateUnbounded<object>();
        var drain = w.DrainAsync(ch.Reader, Write, CancellationToken.None, flushEvery: 1000);

        ch.Writer.TryWrite(new SearchProgress(1, 2, TimeSpan.Zero));
        await WaitUntil(() => pipe.FlushedBytes > 0);
        Assert.That(pipe.Text, Does.StartWith("event: progress\n"));

        ch.Writer.Complete();
        await drain;
    }

    [Test]
    public async Task Bounded_queue_makes_a_slow_consumer_stall_the_producer_without_dropping()
    {
        const int total = 500, capacity = 8, flushEvery = 4;
        var (w, pipe) = Create();
        pipe.FlushGate = new SemaphoreSlim(0); // stalled client: flushes block until released
        var ch = Channel.CreateBounded<object>(new BoundedChannelOptions(capacity)
            { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });

        var produced = 0;
        var producer = Task.Run(async () =>
        {
            for (var i = 0; i < total; i++)
            {
                await ch.Writer.WriteAsync(Row("n" + i, @"C:\x"));
                Interlocked.Increment(ref produced);
            }
            ch.Writer.Complete();
        });
        var count = 0;
        var drain = w.DrainAsync(ch.Reader, (sw, o) => { count++; Write(sw, o); }, CancellationToken.None, flushEvery);

        await Task.Delay(300);
        // At most: one flush batch held by the drain + the queue capacity + one write awaiting space.
        var ahead = Volatile.Read(ref produced);
        TestContext.Out.WriteLine($"stalled client: producer got {ahead} of {total} rows through");
        Assert.That(ahead, Is.LessThanOrEqualTo(capacity + flushEvery + 2),
            "producer must not run ahead of a stalled client");
        Assert.That(producer.IsCompleted, Is.False);

        pipe.FlushGate.Release(10_000); // client resumes
        await producer;
        await drain;
        Assert.That(count, Is.EqualTo(total), "nothing dropped");
        Assert.That(pipe.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(total));
    }

    [Test]
    public void Drain_stops_on_cancellation()
    {
        var (w, _) = Create();
        var ch = Channel.CreateUnbounded<object>();
        using var cts = new CancellationTokenSource();
        var drain = w.DrainAsync(ch.Reader, Write, cts.Token);
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await drain);
    }

    [Test]
    public async Task Drain_stops_when_the_client_has_gone_away()
    {
        var (w, pipe) = Create();
        pipe.CompleteFlushes = true;
        var ch = Channel.CreateUnbounded<object>();
        ch.Writer.TryWrite(Row("a", "b"));
        await w.DrainAsync(ch.Reader, Write, CancellationToken.None); // returns instead of hanging
    }

    [Test]
    public void Writing_rows_makes_no_per_row_intermediate_strings()
    {
        var rows = new SearchResultRow[100];
        for (var i = 0; i < rows.Length; i++) rows[i] = Row("file" + i + ".txt", @"C:\a\file" + i + ".txt");
        var pipe = new TestPipeWriter(discard: true);
        using var w = new SseWriter(pipe);

        void WriteAll()
        {
            foreach (var r in rows) w.Write("result"u8, r, AppCoreJsonContext.Default.SearchResultRow);
            w.FlushAsync().AsTask().GetAwaiter().GetResult();
            pipe.Reset();
        }

        void OldWriteAll()
        {
            foreach (var r in rows)
            {
                var data = JsonSerializer.Serialize(r, AppCoreJsonContext.Default.SearchResultRow);
                var a = $"event: result\n";
                var b = $"data: {data}\n\n";
                GC.KeepAlive(Encoding.UTF8.GetBytes(a));
                GC.KeepAlive(Encoding.UTF8.GetBytes(b));
            }
        }

        for (var i = 0; i < 20; i++) WriteAll(); // warm up (JIT, writer buffers)
        const int rounds = 100;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < rounds; i++) WriteAll();
        var newPerRow = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)(rounds * rows.Length);

        for (var i = 0; i < 20; i++) OldWriteAll();
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < rounds; i++) OldWriteAll();
        var oldPerRow = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)(rounds * rows.Length);

        TestContext.Out.WriteLine($"allocated bytes/row: new={newPerRow:F2} old-string-path={oldPerRow:F2}");
        Assert.That(newPerRow, Is.LessThan(8), "a row must not allocate intermediate JSON/event strings");
        Assert.That(oldPerRow, Is.GreaterThan(500), "sanity: the baseline really allocates");
    }

    [Test]
    public async Task Drain_reports_false_when_the_client_has_gone_away()
    {
        var (w, pipe) = Create();
        pipe.CompleteFlushes = true;
        var ch = Channel.CreateUnbounded<object>();
        ch.Writer.TryWrite(Row("a", "b"));
        Assert.That(await w.DrainAsync(ch.Reader, Write, CancellationToken.None), Is.False);

        var ch2 = Channel.CreateUnbounded<object>();
        ch2.Writer.Complete();
        Assert.That(await w.DrainAsync(ch2.Reader, Write, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task Stream_skips_done_when_the_client_has_gone_away()
    {
        var pipe = new TestPipeWriter { CompleteFlushes = true };
        var doneWritten = false;
        await SseStream.RunAsync(pipe,
            async (_, q, ct) => await q.WriteAsync(Row("a", "b"), ct),
            (w, _) => doneWritten = true, CancellationToken.None);
        Assert.That(doneWritten, Is.False);
        Assert.That(pipe.Text, Does.Not.Contain("event: done"));
    }

    [Test]
    public async Task Final_progress_dropped_by_a_full_queue_is_still_delivered_before_done()
    {
        var pipe = new TestPipeWriter { FlushGate = new SemaphoreSlim(0) }; // stalled client
        var final = new SearchProgress(10, 10, TimeSpan.FromSeconds(1));
        var reported = new TaskCompletionSource();

        var run = SseStream.RunAsync(pipe,
            async (progress, q, ct) =>
            {
                await q.WriteAsync(Row("r1", @"C:\x"), ct); // drain takes it and blocks in its flush (flushEvery = 1)
                await q.WriteAsync(Row("r2", @"C:\x"), ct); // fills the capacity-1 queue
                progress.Report(new SearchProgress(5, 10, TimeSpan.Zero)); // rejected: queue full
                progress.Report(final); // rejected too; newest must win
                reported.SetResult();
            },
            (w, count) => w.WriteDone(count), CancellationToken.None, capacity: 1, flushEvery: 1);

        await reported.Task;
        pipe.FlushGate.Release(1000);
        await run;

        var events = pipe.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.That(events, Has.Length.EqualTo(4));
        Assert.That(events[0], Does.StartWith("event: result"));
        Assert.That(events[1], Does.StartWith("event: result"));
        Assert.That(events[2], Is.EqualTo("event: progress\ndata: " +
            JsonSerializer.Serialize(final, AppCoreJsonContext.Default.SearchProgress)));
        Assert.That(events[3], Is.EqualTo("event: done\ndata: {\"count\":2}"));
    }

    [Test]
    public async Task Stream_observes_a_producer_failure_after_sending_done()
    {
        var pipe = new TestPipeWriter();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SseStream.RunAsync(pipe,
            (_, _, _) => throw new InvalidOperationException("boom"),
            (w, c) => w.WriteDone(c), CancellationToken.None));
        Assert.That(ex.Message, Is.EqualTo("boom"));
        Assert.That(pipe.Text, Is.EqualTo("event: done\ndata: {\"count\":0}\n\n")); // unchanged from develop
    }

    [Test]
    public async Task Stream_cancellation_releases_a_producer_waiting_on_a_full_queue()
    {
        var pipe = new TestPipeWriter { FlushGate = new SemaphoreSlim(0) };
        using var cts = new CancellationTokenSource();
        var run = SseStream.RunAsync(pipe,
            async (_, q, ct) =>
            {
                for (var i = 0; i < 100; i++) await q.WriteAsync(Row("r", "p"), ct);
            },
            (w, c) => w.WriteDone(c), cts.Token, capacity: 1, flushEvery: 1);
        await Task.Delay(100);
        cts.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(5)); // completes without throwing
    }

    [Test]
    public void A_failed_serialization_leaves_no_partial_frame_in_the_pipe()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new ThrowingConverter() },
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        };
        var info = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Boom>)options.GetTypeInfo(typeof(Boom));
        var (w, pipe) = Create();
        w.Write("result"u8, Row("before", "p"), AppCoreJsonContext.Default.SearchResultRow);
        Assert.Throws<InvalidOperationException>(() => w.Write("result"u8, new Boom(), info));
        w.Write("result"u8, Row("after", "p"), AppCoreJsonContext.Default.SearchResultRow);
        w.FlushAsync().AsTask().GetAwaiter().GetResult();

        var expected = OldFormat("result", JsonSerializer.Serialize(Row("before", "p"), AppCoreJsonContext.Default.SearchResultRow))
                       + OldFormat("result", JsonSerializer.Serialize(Row("after", "p"), AppCoreJsonContext.Default.SearchResultRow));
        Assert.That(pipe.Text, Is.EqualTo(expected));
    }

    [Test]
    public void Unknown_queue_items_are_rejected_not_silently_dropped()
    {
        var (w, _) = Create();
        Assert.Throws<InvalidOperationException>(() => SseQueue.WriteItem(w, "surprise"));
    }

    private sealed class Boom;

    private sealed class ThrowingConverter : System.Text.Json.Serialization.JsonConverter<Boom>
    {
        public override Boom Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, Boom value, JsonSerializerOptions o)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("half");
            throw new InvalidOperationException("mid-serialization failure");
        }
    }

    private static void Write(SseWriter w, object item)
    {
        switch (item)
        {
            case SearchResultRow r: w.Write("result"u8, r, AppCoreJsonContext.Default.SearchResultRow); break;
            case SearchProgress p: w.Write("progress"u8, p, AppCoreJsonContext.Default.SearchProgress); break;
        }
    }

    private static async Task WaitUntil(Func<bool> cond)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond())
        {
            if (sw.Elapsed > TimeSpan.FromSeconds(5)) Assert.Fail("timed out");
            await Task.Delay(5);
        }
    }
}

/// <summary>In-memory PipeWriter recording bytes and flush behaviour; optionally gates flushes (stalled client).</summary>
internal sealed class TestPipeWriter(bool discard = false) : PipeWriter
{
    private byte[] _buf = new byte[64 * 1024];
    private int _len;
    public int FlushedBytes { get; private set; }
    public int FlushCount { get; private set; }
    public int Length => _len;
    public SemaphoreSlim FlushGate { get; set; }
    public bool CompleteFlushes { get; set; }
    public string Text => Encoding.UTF8.GetString(_buf, 0, _len);

    public void Reset() { _len = 0; FlushedBytes = 0; }

    public override void Advance(int bytes) => _len += bytes;

    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buf.AsMemory(_len);
    }

    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buf.AsSpan(_len);
    }

    private void Ensure(int hint)
    {
        var need = Math.Max(hint, 1);
        if (_buf.Length - _len >= need) return;
        Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + need));
    }

    public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (FlushGate != null) await FlushGate.WaitAsync(cancellationToken);
        FlushCount++;
        FlushedBytes = _len;
        if (discard) Reset();
        return new FlushResult(false, CompleteFlushes);
    }

    public override void Complete(Exception exception = null) { }
    public override void CancelPendingFlush() { }
}

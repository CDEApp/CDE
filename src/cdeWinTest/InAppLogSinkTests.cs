using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeWin;
using NUnit.Framework;
using Serilog;

namespace cdeWinTest;

[TestFixture]
public class InAppLogSinkTests
{
    private static (InAppLogSink sink, ILogger log) Create(int maxLines = 100)
    {
        var sink = new InAppLogSink(maxLines);
        var log = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        return (sink, log);
    }

    [Test]
    public void EventsBeforeAttach_AreBuffered_AndNotifiedOnAttach()
    {
        var (sink, log) = Create();
        log.Debug("CDE Starting");
        log.Information("Loaded {Count} items", 3);

        var notified = 0;
        sink.Attach(() => notified++);

        Assert.That(notified, Is.EqualTo(1));
        var lines = sink.Drain();
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines[0], Does.Contain("CDE Starting"));
        Assert.That(lines[1], Does.Contain("Loaded 3 items"));
    }

    [Test]
    public void Attach_WithNothingBuffered_DoesNotNotify()
    {
        var (sink, _) = Create();
        var notified = 0;
        sink.Attach(() => notified++);
        Assert.That(notified, Is.EqualTo(0));
    }

    [Test]
    public void EventsAfterAttach_NotifyOncePerDrain()
    {
        var (sink, log) = Create();
        var notified = 0;
        sink.Attach(() => notified++);

        log.Information("one");
        log.Information("two");
        Assert.That(notified, Is.EqualTo(1));

        Assert.That(sink.Drain(), Has.Length.EqualTo(2));
        Assert.That(sink.Drain(), Is.Empty);

        log.Information("three");
        Assert.That(notified, Is.EqualTo(2));
    }

    [Test]
    public void Cap_KeepsOnlyNewestLines()
    {
        var (sink, log) = Create(maxLines: 3);
        for (var i = 1; i <= 10; i++)
        {
            log.Information("line {N}", i);
        }

        var lines = sink.Drain();
        Assert.That(lines, Has.Length.EqualTo(3));
        Assert.That(lines[0], Does.Contain("line 8"));
        Assert.That(lines[2], Does.Contain("line 10"));
    }

    [Test]
    public void Format_HasTimestampLevelMessageAndException()
    {
        var (sink, log) = Create();
        log.Error(new InvalidOperationException("boom"), "Failed {What}", "load");

        var line = sink.Drain().Single();
        Assert.That(line, Does.Match(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} ERR Failed ""load"""));
        Assert.That(line, Does.Contain("InvalidOperationException"));
        Assert.That(line, Does.Contain("boom"));
    }

    [Test]
    public void Format_LevelAbbreviations()
    {
        var (sink, log) = Create();
        log.Debug("d");
        log.Information("i");
        log.Warning("w");
        var lines = sink.Drain();
        Assert.That(lines[0], Does.Contain(" DBG d"));
        Assert.That(lines[1], Does.Contain(" INF i"));
        Assert.That(lines[2], Does.Contain(" WRN w"));
    }

    [Test]
    public void Detach_StopsNotifications_ButStillBuffers()
    {
        var (sink, log) = Create();
        var notified = 0;
        sink.Attach(() => notified++);
        sink.Detach();
        log.Information("x");
        Assert.That(notified, Is.EqualTo(0));
        Assert.That(sink.Drain(), Has.Length.EqualTo(1));
    }

    [Test]
    public void ConcurrentEmit_IsThreadSafe_AndRespectsCap()
    {
        var (sink, log) = Create(maxLines: 500);
        var notifications = 0;
        sink.Attach(() => Interlocked.Increment(ref notifications));

        var drained = new List<string>();
        var done = false;
        var drainer = Task.Run(() =>
        {
            while (!Volatile.Read(ref done))
            {
                drained.AddRange(sink.Drain());
            }
        });

        Parallel.For(0, 8, t =>
        {
            for (var i = 0; i < 2000; i++)
            {
                log.Information("t{T} i{I}", t, i);
            }
        });

        Volatile.Write(ref done, true);
        drainer.Wait();
        drained.AddRange(sink.Drain());

        Assert.That(drained.Count, Is.LessThanOrEqualTo(16000));
        Assert.That(drained.Count, Is.GreaterThan(0));
        Assert.That(drained.All(l => l.Contains(" INF t")), Is.True);
        Assert.That(Volatile.Read(ref notifications), Is.GreaterThan(0));
    }
}

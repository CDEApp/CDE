using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// The work-stealing <see cref="FindOptions.FindAsync"/> path: matching, limit, cancellation, and
/// progress reporting over a tree large enough to hit the periodic yield and progress checkpoints.
/// </summary>
[TestFixture]
public class FindAsyncTests
{
    private const int FileCount = 5000;

    private static RootEntry BuildRoot()
    {
        var root = new RootEntry { Path = @"C:\find", VolumeName = "V", DefaultFileName = "f.cde" };
        for (var d = 0; d < 5; d++)
        {
            var dir = new DirEntry(true) { Path = $"dir{d}" };
            for (var i = 0; i < FileCount / 5; i++)
            {
                var name = i % 250 == 0 ? $"needle{d}_{i}.txt" : $"file{d}_{i}.dat";
                dir.AddChild(new DirEntry(false) { Path = name, Size = i });
            }

            root.AddChild(dir);
        }

        root.SetInMemoryFields();
        return root;
    }

    private sealed class CapturingProgress : IProgress<SearchProgress>
    {
        public readonly ConcurrentBag<SearchProgress> Reports = [];
        public void Report(SearchProgress value) => Reports.Add(value);
    }

    private static FindOptions Options(ConcurrentBag<string> hits) => new()
    {
        Pattern = "needle",
        IncludeFiles = true,
        IncludeFolders = false,
        VisitorFunc = (_, entry) =>
        {
            hits.Add(entry.Path);
            return true;
        },
    };

    [Test]
    public async Task FindAsync_VisitsEveryMatchingEntry()
    {
        var hits = new ConcurrentBag<string>();
        var options = Options(hits);

        await options.FindAsync([BuildRoot()]);

        Assert.That(hits, Has.Count.EqualTo(5 * (FileCount / 5 / 250)));
        Assert.That(hits, Has.All.StartsWith("needle"));
    }

    [Test]
    public async Task FindAsync_NoVisitor_DoesNothing()
    {
        var options = new FindOptions { Pattern = "needle" };

        await options.FindAsync([BuildRoot()]);

        Assert.That(options.ProgressCount, Is.Zero);
    }

    [Test]
    public async Task FindAsync_LimitResultCount_StopsEarly()
    {
        var hits = new ConcurrentBag<string>();
        var options = Options(hits);
        options.LimitResultCount = 3;

        await options.FindAsync([BuildRoot()]);

        Assert.That(hits, Is.Not.Empty);
        Assert.That(hits.Count, Is.LessThan(5 * (FileCount / 5 / 250)));
    }

    [Test]
    public async Task FindAsync_VisitorReturningFalse_StopsTheSearch()
    {
        var calls = 0;
        var options = Options([]);
        options.VisitorFunc = (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return false;
        };

        await options.FindAsync([BuildRoot()]);

        Assert.That(calls, Is.GreaterThan(0));
        Assert.That(calls, Is.LessThan(5 * (FileCount / 5 / 250)));
    }

    [Test]
    public async Task FindAsync_AlreadyCancelled_CompletesWithoutThrowing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var options = Options([]);

        await options.FindAsync([BuildRoot()], cts.Token);

        Assert.That(options.CancellationToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public async Task FindAsync_SkipCount_SkipsLeadingEntries()
    {
        var hits = new ConcurrentBag<string>();
        var options = Options(hits);
        options.SkipCount = FileCount * 10; // more than the whole tree

        await options.FindAsync([BuildRoot()]);

        Assert.That(hits, Is.Empty);
    }

    [Test]
    public async Task FindAsync_ReportsProgressAndFinishesAtTotal()
    {
        var reports = new List<(int Count, int Total)>();
        var progress = new CapturingProgress();
        var options = Options([]);
        options.ProgressModifier = 1;
        options.ProgressFunc = (count, total) =>
        {
            lock (reports) reports.Add((count, total));
        };
        options.Progress = progress;

        await options.FindAsync([BuildRoot()]);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (progress.Reports.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        lock (reports)
        {
            Assert.That(reports[0].Count, Is.Zero, "start-of-search report");
            Assert.That(reports[^1].Count, Is.EqualTo(options.ProgressEnd), "end-of-search report is always 100%");
        }

        Assert.That(progress.Reports, Is.Not.Empty, "a structured progress report was streamed");
    }

    [Test]
    public async Task FindAsync_CancelledMidSearch_StopsBeforeVisitingEverything()
    {
        using var cts = new CancellationTokenSource();
        var seen = 0;
        var options = Options([]);
        options.Pattern = "";
        options.VisitorFunc = (_, _) =>
        {
            if (Interlocked.Increment(ref seen) == 10)
            {
                cts.Cancel();
            }

            return true;
        };

        await options.FindAsync([BuildRoot()], cts.Token);

        Assert.That(seen, Is.GreaterThanOrEqualTo(10));
    }
}

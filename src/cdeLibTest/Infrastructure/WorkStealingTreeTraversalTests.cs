using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure;

/// <summary>A failure inside the per-entry processor must reach the caller, not be logged and swallowed (#171).</summary>
[TestFixture]
public class WorkStealingTreeTraversalTests
{
    private static RootEntry RootWithFiles(int count)
    {
        var root = new RootEntry { Children = new List<DirEntry>() };
        for (var i = 0; i < count; i++) root.Children.Add(new DirEntry(false) { Path = "f" + i });
        return root;
    }

    [Test]
    public async Task TraverseAsync_ProcessorThrows_ExceptionPropagates()
    {
        var traversal = new WorkStealingTreeTraversal(2);

        Task<bool> Fail(ICommonEntry parent, ICommonEntry child) => throw new InvalidOperationException("boom");

        await Assert.ThatAsync(() => traversal.TraverseAsync([RootWithFiles(3)], Fail),
            Throws.InvalidOperationException.With.Message.EqualTo("boom"));
    }

    [Test]
    public async Task TraverseAsync_ProcessorSucceeds_VisitsEveryChild()
    {
        var traversal = new WorkStealingTreeTraversal(2);
        var visited = 0;

        await traversal.TraverseAsync([RootWithFiles(5)], (_, _) =>
        {
            System.Threading.Interlocked.Increment(ref visited);
            return Task.FromResult(true);
        });

        Assert.That(visited, Is.EqualTo(5));
    }

    [Test]
    public async Task TraverseAsync_ProcessorThrows_StopsOtherWorkersPromptly()
    {
        const int dirs = 100, filesPerDir = 200;
        var root = new RootEntry { Children = new List<DirEntry>() };
        for (var d = 0; d < dirs; d++)
        {
            var dir = new DirEntry(true) { Path = "d" + d, Children = new List<DirEntry>() };
            for (var f = 0; f < filesPerDir; f++) dir.Children.Add(new DirEntry(false) { Path = "f" + f });
            root.Children.Add(dir);
        }

        var traversal = new WorkStealingTreeTraversal(4);
        var calls = 0;

        async Task<bool> Process(ICommonEntry parent, ICommonEntry child)
        {
            var n = System.Threading.Interlocked.Increment(ref calls);
            await Task.Yield();
            System.Threading.Thread.SpinWait(5000);
            if (n == 500) throw new InvalidOperationException("boom");
            return true;
        }

        await Assert.ThatAsync(() => traversal.TraverseAsync([root], Process),
            Throws.InvalidOperationException.With.Message.EqualTo("boom"));
        Assert.That(calls, Is.LessThan(dirs * filesPerDir / 2), "other workers kept traversing after the failure");
    }
}

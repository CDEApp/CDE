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
}

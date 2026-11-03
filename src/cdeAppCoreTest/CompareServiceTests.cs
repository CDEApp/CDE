using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using cdeAppCore;
using cdeAppCore.Compare;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace cdeAppCoreTest;

[TestFixture]
public class CompareServiceTests
{
    private static readonly DateTime T0 = new(2024, 5, 1, 12, 0, 0, DateTimeKind.Unspecified);

    private static DirEntry D(string name, params DirEntry[] children)
    {
        var d = new DirEntry(true) { Path = name, Modified = T0 };
        foreach (var c in children) d.AddChild(c);
        return d;
    }

    private static DirEntry F(string name, long size, double offsetMs = 0, bool badDate = false)
    {
        var f = new DirEntry(false) { Path = name, Size = size, Modified = T0.AddMilliseconds(offsetMs) };
        if (badDate) f.IsModifiedBad = true;
        return f;
    }

    private static RootEntry Root(params DirEntry[] children)
    {
        var root = new RootEntry { Path = @"C:\t", VolumeName = "V", DefaultFileName = "t.cde", ActualFileName = "t.cde" };
        foreach (var c in children) root.AddChild(c);
        root.SetInMemoryFields();
        return root;
    }

    /// <summary>Catalogs loaded in the given order, so the first tree is catalog 0.</summary>
    private sealed class Catalogs : IDisposable
    {
        private readonly List<string> _paths = [];
        public CatalogSession Session { get; }

        public Catalogs(params RootEntry[] trees)
        {
            foreach (var tree in trees)
            {
                var path = Path.Combine(Path.GetTempPath(), $"cdecompare-{Guid.NewGuid():N}.cdex");
                ColumnarFormat.Write(EntryStore.Build(tree), path);
                _paths.Add(path);
            }

            var loader = Substitute.For<ILoadCatalogService>();
            loader.GetColumnarFiles(Arg.Any<string>()).Returns(_paths.ToList());
            Session = new CatalogSession(loader, Substitute.For<ILogger>());
            Session.LoadAsync("config").GetAwaiter().GetResult();
        }

        /// <summary>Ref of the entry at a slash-separated path below the catalog root.</summary>
        public EntryRefDto Ref(int catalog, string path = "")
        {
            var current = new EntryRefDto(catalog, 0);
            foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
                current = Session.GetChildren(current).Single(n => n.Name == part).Ref;
            return current;
        }

        public void Dispose()
        {
            Session.Dispose();
            foreach (var p in _paths)
            {
                try { File.Delete(p); }
                catch { /* best effort */ }
            }
        }
    }

    private static CompareSummaryDto Compare(Catalogs c, EntryRefDto left, EntryRefDto right) =>
        new CompareService(c.Session).Summarize([(left, right)]).Single();

    private static CompareSummaryDto CompareRoots(RootEntry left, RootEntry right)
    {
        using var c = new Catalogs(left, right);
        return Compare(c, c.Ref(0), c.Ref(1));
    }

    private static void AssertCounts(CompareSummaryDto s, int same = 0, int different = 0, int leftNewer = 0,
        int rightNewer = 0, int leftOnly = 0, int rightOnly = 0)
    {
        Assert.That((s.SameFiles, s.DifferentFiles, s.LeftNewerFiles, s.RightNewerFiles, s.LeftOnly, s.RightOnly),
            Is.EqualTo((same, different, leftNewer, rightNewer, leftOnly, rightOnly)));
    }

    [Test]
    public void IdenticalSubtrees_OnlySameFiles()
    {
        var s = CompareRoots(
            Root(F("a.txt", 1), D("sub", F("b.txt", 2), D("deep", F("c.txt", 3)))),
            Root(F("a.txt", 1), D("sub", F("b.txt", 2), D("deep", F("c.txt", 3)))));
        AssertCounts(s, same: 3);
    }

    [Test]
    public void ChangedSize_IsDifferentWithNeitherNewer()
    {
        var s = CompareRoots(Root(F("a.txt", 1)), Root(F("a.txt", 2)));
        AssertCounts(s, different: 1);
    }

    [Test]
    public void LeftModifiedBeyondTolerance_IsDifferentAndLeftNewer()
    {
        var s = CompareRoots(Root(F("a.txt", 1, 2001)), Root(F("a.txt", 1)));
        AssertCounts(s, different: 1, leftNewer: 1);
    }

    [Test]
    public void RightModifiedBeyondTolerance_IsDifferentAndRightNewer()
    {
        var s = CompareRoots(Root(F("a.txt", 1)), Root(F("a.txt", 1, 5000)));
        AssertCounts(s, different: 1, rightNewer: 1);
    }

    [Test]
    public void ModifiedExactlyAtOrWithinTolerance_IsSame()
    {
        var s = CompareRoots(
            Root(F("a.txt", 1, 2000), F("b.txt", 1, 1)),
            Root(F("a.txt", 1), F("b.txt", 1, 1500)));
        AssertCounts(s, same: 2);
    }

    [Test]
    public void SizeDiffersAndDateWithinTolerance_IsDifferentNeitherNewer()
    {
        var s = CompareRoots(Root(F("a.txt", 1, 500)), Root(F("a.txt", 2)));
        AssertCounts(s, different: 1);
    }

    [Test]
    public void BadDates_SameOnlyWhenBothBad_AndNeverNewer()
    {
        var s = CompareRoots(
            Root(F("both.txt", 1, badDate: true), F("one.txt", 1, badDate: true)),
            Root(F("both.txt", 1, badDate: true), F("one.txt", 1, 99999)));
        AssertCounts(s, same: 1, different: 1);
    }

    [Test]
    public void OneSidedFilesAndFolders_CountOnce_AndAreNotDescended()
    {
        var s = CompareRoots(
            Root(F("keep.txt", 1), F("lonely.txt", 1), D("leftdir", F("x", 1), F("y", 1), D("z", F("w", 1)))),
            Root(F("keep.txt", 1), D("rightdir", F("p", 1), F("q", 1))));
        AssertCounts(s, same: 1, leftOnly: 2, rightOnly: 1);
    }

    [Test]
    public void TwoCaseVariantsOnOneSideWithNothingOnTheOther_AreBothOneSided()
    {
        var s = CompareRoots(Root(F("Foo.txt", 1), F("foo.txt", 1)), Root(F("unrelated.txt", 1)));
        AssertCounts(s, leftOnly: 2, rightOnly: 1);
    }

    [Test]
    public void TwoCaseVariantsLeftAgainstOneRight_PairsExactNameFirstAndLeavesTheOtherOneSided()
    {
        var s = CompareRoots(Root(F("Foo.txt", 1), F("foo.txt", 9)), Root(F("foo.txt", 9)));
        AssertCounts(s, same: 1, leftOnly: 1);
    }

    [Test]
    public void SingleCaseVariantOnEachSide_Pairs()
    {
        var s = CompareRoots(Root(F("Foo.txt", 1)), Root(F("foo.txt", 1)));
        AssertCounts(s, same: 1);
    }

    [Test]
    public void SameFoldedNameInOrder_PairsLeftoversPositionally()
    {
        var s = CompareRoots(Root(F("AB.txt", 1), F("Ab.txt", 2)), Root(F("aB.txt", 1), F("ab.txt", 3)));
        AssertCounts(s, same: 1, different: 1);
    }

    [Test]
    public void MismatchedKinds_AreDifferentAndNotDescended()
    {
        var s = CompareRoots(
            Root(F("thing", 5), D("other", F("inside", 1))),
            Root(D("thing", F("inside", 1)), F("other", 5)));
        AssertCounts(s, different: 2);
    }

    [Test]
    public void EmptyFolders_AllZero()
    {
        AssertCounts(CompareRoots(Root(), Root()));
        AssertCounts(CompareRoots(Root(D("e")), Root(D("e"))));
    }

    [Test]
    public void EmptyLeftSide_EverythingOnTheRightIsRightOnly()
    {
        AssertCounts(CompareRoots(Root(), Root(F("a", 1), D("d", F("b", 1)))), rightOnly: 2);
    }

    [Test]
    public void BothRefsInTheSameCatalog_Compare()
    {
        using var c = new Catalogs(Root(
            D("a", F("x", 1), F("y", 2), F("only-a", 1)),
            D("b", F("x", 1), F("y", 3, 9000))));
        var s = Compare(c, c.Ref(0, "a"), c.Ref(0, "b"));
        AssertCounts(s, same: 1, different: 1, rightNewer: 1, leftOnly: 1);
    }

    [Test]
    public void SameFolderAgainstItself_IsAllSame()
    {
        using var c = new Catalogs(Root(D("a", F("x", 1), D("s", F("y", 2)))));
        var a = c.Ref(0, "a");
        AssertCounts(Compare(c, a, a), same: 2);
    }

    [Test]
    public void SeveralPairs_ReturnSummariesInRequestOrder()
    {
        using var c = new Catalogs(
            Root(D("p", F("x", 1)), D("q", F("x", 1), F("y", 1)), D("r")),
            Root(D("p", F("x", 1)), D("q", F("x", 5)), D("r", F("z", 1))));
        var pairs = new[]
        {
            (c.Ref(0, "r"), c.Ref(1, "r")),
            (c.Ref(0, "q"), c.Ref(1, "q")),
            (c.Ref(0, "p"), c.Ref(1, "p")),
        };
        var summaries = new CompareService(c.Session).Summarize(pairs);

        Assert.That(summaries, Has.Count.EqualTo(3));
        AssertCounts(summaries[0], rightOnly: 1);
        AssertCounts(summaries[1], different: 1, leftOnly: 1);
        AssertCounts(summaries[2], same: 1);
    }

    [Test]
    public void ManyPairsInParallel_KeepOrder()
    {
        var left = Enumerable.Range(0, 60).Select(i => D($"d{i}", Enumerable.Range(0, i).Select(j => F($"f{j}", 1)).ToArray())).ToArray();
        var right = Enumerable.Range(0, 60).Select(i => D($"d{i}")).ToArray();
        using var c = new Catalogs(Root(left), Root(right));
        var pairs = Enumerable.Range(0, 60).Select(i => (c.Ref(0, $"d{i}"), c.Ref(1, $"d{i}"))).ToArray();

        var summaries = new CompareService(c.Session).Summarize(pairs);

        Assert.That(summaries.Select(s => s.LeftOnly), Is.EqualTo(Enumerable.Range(0, 60)));
    }

    [Test]
    public void DeepTree_DoesNotOverflowTheStack()
    {
        DirEntry Chain(int depth)
        {
            var leaf = D("n", F("f", 1));
            for (var i = 0; i < depth; i++) leaf = D("n", leaf);
            return leaf;
        }

        // The columnar writer and loader recurse nowhere near this deep; the walk itself must not either.
        using var c = new Catalogs(Root(Chain(300)), Root(Chain(300)));
        AssertCounts(Compare(c, c.Ref(0), c.Ref(1)), same: 1);
    }

    [Test]
    public void NoPairs_ReturnsNoSummaries()
    {
        using var c = new Catalogs(Root());
        Assert.That(new CompareService(c.Session).Summarize([]), Is.Empty);
    }

    [Test]
    public void NonDirectory_IsBadRequest()
    {
        using var c = new Catalogs(Root(F("a.txt", 1), D("d")));
        var ex = Assert.Throws<CompareRequestException>(() =>
            new CompareService(c.Session).Summarize([(c.Ref(0, "a.txt"), c.Ref(0, "d"))]));
        Assert.That(ex.Error, Is.EqualTo(CompareRequestError.BadRequest));
    }

    [Test]
    public void EntryPastTheCatalog_IsBadRequest()
    {
        using var c = new Catalogs(Root(D("d")));
        var ex = Assert.Throws<CompareRequestException>(() =>
            new CompareService(c.Session).Summarize([(c.Ref(0, "d"), new EntryRefDto(0, 100000))]));
        Assert.That(ex.Error, Is.EqualTo(CompareRequestError.BadRequest));
    }

    [Test]
    public void UnknownCatalog_IsNotFound()
    {
        using var c = new Catalogs(Root(D("d")));
        var ex = Assert.Throws<CompareRequestException>(() =>
            new CompareService(c.Session).Summarize([(c.Ref(0, "d"), new EntryRefDto(7, 0))]));
        Assert.That(ex.Error, Is.EqualTo(CompareRequestError.NotFound));
    }

    [Test]
    public void MoreThanMaxPairs_IsBadRequest()
    {
        using var c = new Catalogs(Root(D("d")));
        var d = c.Ref(0, "d");
        var pairs = Enumerable.Repeat((d, d), CompareService.MaxPairs + 1).ToArray();
        var ex = Assert.Throws<CompareRequestException>(() => new CompareService(c.Session).Summarize(pairs));
        Assert.That(ex.Error, Is.EqualTo(CompareRequestError.BadRequest));

        Assert.That(new CompareService(c.Session).Summarize(pairs.Take(CompareService.MaxPairs).ToArray()),
            Has.Count.EqualTo(CompareService.MaxPairs));
    }

    [Test]
    public void CancelledToken_StopsTheWalk()
    {
        using var c = new Catalogs(Root(D("d", F("x", 1))), Root(D("d", F("x", 1))));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.That(() => new CompareService(c.Session).Summarize([(c.Ref(0, "d"), c.Ref(1, "d"))], cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void LeasesAreReleased_SoARescanSwapIsNotBlockedAfterwards()
    {
        using var c = new Catalogs(Root(D("a")), Root(D("a")));
        var service = new CompareService(c.Session);
        service.Summarize([(c.Ref(0, "a"), c.Ref(1, "a"))]);
        Assert.That(() => service.Summarize([(c.Ref(0, "a"), c.Ref(1, "a"))]), Throws.Nothing);
        // A leaked read lease would make a writer wait forever; entering and leaving a fresh lease proves none is held.
        Assert.That(() => c.Session.AcquireRead(0).Dispose(), Throws.Nothing);
    }
}

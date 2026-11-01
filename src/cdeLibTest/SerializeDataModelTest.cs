using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Serialization;
using NSubstitute;

namespace cdeLibTest;

/// <summary>
/// Test out how it serializes.
/// That it serializes.
/// If i can deserialize without re-hydrating entire tree.
/// </summary>
[TestFixture]
internal class SerializeDataModelTest
{
    // ReSharper disable InconsistentNaming
    private DirEntry de2a;
    private DirEntry de2b;
    private DirEntry de2c;
    private DirEntry de3a;
    private DirEntry de4a;
    private RootEntry re1;
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    [SetUp]
    public void SetUp()
    {
        _config.ProgressUpdateInterval.Returns(5000);
    }

    // just a sanity check - shouldn't ever fail and only checking rootpath and name field.
    [Test]
    public void Serialize_Deserialize_RootEntryMatches()
    {
        re1 = RootEntryTestBase.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);

        var b = MessagePack.MessagePackSerializer.Serialize(re1, MessagePackConfig.Options);

        var re2 = MessagePack.MessagePackSerializer.Deserialize<RootEntry>(b, MessagePackConfig.Options);

        var same = re1.SameTree(re2);
        Assert.That(same, Is.True, TreeDuplicate.LastMessage);
    }

    [Test]
    public void Serialize_Deserialize_RootEntryFailsMatches()
    {
        re1 = RootEntryTestBase.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);

        var b = MessagePack.MessagePackSerializer.Serialize(re1, MessagePackConfig.Options);

        var re2 = MessagePack.MessagePackSerializer.Deserialize<RootEntry>(b, MessagePackConfig.Options);

        re2.Path = "moo";
        var same = re1.SameTree(re2);
        Assert.That(same, Is.False, TreeDuplicate.LastMessage);
    }

    // The legacy .cde format is MessagePack with the custom resolver (Hash16 formatter); guards
    // package upgrades against silently changing the round trip.
    [Test]
    public void MessagePack_RoundTrip_PreservesTreeAndHashes()
    {
        re1 = RootEntryTestBase.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);
        de2a.SetHash([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        de2a.IsPartialHash = true;

        var bytes = MessagePack.MessagePackSerializer.Serialize(re1,
            cdeLib.Infrastructure.Serialization.MessagePackConfig.Options);
        var re2 = MessagePack.MessagePackSerializer.Deserialize<RootEntry>(bytes,
            cdeLib.Infrastructure.Serialization.MessagePackConfig.Options);

        Assert.That(re1.SameTree(re2), Is.True, TreeDuplicate.LastMessage);
        var hashed = re2.Children.SelectMany(c => (c.Children ?? []).Prepend(c))
            .Single(c => c.Path == de2a.Path);
        Assert.That(hashed.IsHashDone, Is.True);
        Assert.That(hashed.IsPartialHash, Is.True);
        Assert.That(hashed.Hash, Is.EqualTo(de2a.Hash));
    }

    // The FlatBuffers protocol maps the tree to FlatSharp 7 types generated from Catalog.fbs, so a
    // field missed by the mapper would be silently dropped.
    [Test]
    public void FlatBuffers_RoundTrip_PreservesTreeHashesAndRootFields()
    {
        re1 = RootEntryTestBase.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);
        de2a.SetHash([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        de2a.IsPartialHash = true;
        de2a.Size = 1234;
        de2a.ModifiedTicks = 638_000_000_000_000_000;
        re1.Description = "desc";
        re1.VolumeName = "vol";
        re1.DefaultFileName = "C-vol-C_.cde";
        re1.PathsWithUnauthorisedExceptions = ["C:\\denied"];
        re1.AvailSpace = 10;
        re1.TotalSpace = 20;
        re1.ScanStartUtcTicks = 30;
        re1.ScanEndUtcTicks = 40;

        var bytes = cdeLib.Catalog.FlatBuffers.FlatBufferCatalogSerializer.Serialize(re1);
        var re2 = cdeLib.Catalog.FlatBuffers.FlatBufferCatalogSerializer.Deserialize(bytes);

        Assert.That(re1.SameTree(re2), Is.True, TreeDuplicate.LastMessage);
        Assert.That(re2.Description, Is.EqualTo("desc"));
        Assert.That(re2.VolumeName, Is.EqualTo("vol"));
        Assert.That(re2.DefaultFileName, Is.EqualTo("C-vol-C_.cde"));
        Assert.That(re2.PathsWithUnauthorisedExceptions, Is.EqualTo(new[] { "C:\\denied" }));
        Assert.That(re2.AvailSpace, Is.EqualTo(10));
        Assert.That(re2.TotalSpace, Is.EqualTo(20));
        Assert.That(re2.ScanStartUtcTicks, Is.EqualTo(30));
        Assert.That(re2.ScanEndUtcTicks, Is.EqualTo(40));
        Assert.That(re2.Version, Is.EqualTo(re1.Version));

        var hashed = re2.Children.SelectMany(c => (c.Children ?? []).Prepend(c))
            .Single(c => c.Path == de2a.Path);
        Assert.That(hashed.IsHashDone, Is.True);
        Assert.That(hashed.IsPartialHash, Is.True);
        Assert.That(hashed.Hash, Is.EqualTo(de2a.Hash));
        Assert.That(hashed.Size, Is.EqualTo(1234));
        Assert.That(hashed.ModifiedTicks, Is.EqualTo(638_000_000_000_000_000));

        // A file has no child list; it must not come back as an empty one.
        var file = re2.Children.SelectMany(c => (c.Children ?? []).Prepend(c)).First(c => !c.IsDirectory);
        Assert.That(file.Children, Is.Null);
    }

    // ReSharper restore InconsistentNaming
}

public static class TreeDuplicate
{
    public static string LastMessage { get; set; }

    public static bool SameTree(this RootEntry re1, RootEntry re2)
    {
        LastMessage = "";

        if (re1.Path != re2.Path)
        {
            LastMessage = $"RootPath {re1.Path} not same as {re2.Path}";
            return false;
        }

        var differenceFound = false;
        var e1 = new DirEntryEnumerator(re1);
        var e2 = new DirEntryEnumerator(re2);
        while (e1.MoveNext() && e2.MoveNext())
        {
            LastMessage = $"{e1.Current.Path} not same as {e2.Current.Path}";
            if (e1.Current.Path != e2.Current.Path)
            {
                differenceFound = true;
                break;
            }
            Console.WriteLine(LastMessage);
        }
        var bothTreesComplete = !e1.MoveNext() && !e2.MoveNext();
        return !differenceFound && bothTreesComplete;
    }
}

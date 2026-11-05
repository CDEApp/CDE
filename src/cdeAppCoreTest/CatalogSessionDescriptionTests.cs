using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace cdeAppCoreTest;

/// <summary>Editing a catalog's description rewrites only the .cdex metadata and swaps it in like a rescan.</summary>
[TestFixture]
public class CatalogSessionDescriptionTests
{
    private string _dir;
    private string _cdex;
    private CatalogSession _session;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-desc-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(data, "sub"));
        File.WriteAllText(Path.Combine(data, "a.txt"), "a");
        File.WriteAllText(Path.Combine(data, "sub", "b.txt"), "bb");

        var re = new RootEntry();
        re.PopulateRoot(data, followJunctions: true);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        re.Description = "first";
        _cdex = Path.Combine(_dir, "one.cdex");
        re.ActualFileName = _cdex;
        ColumnarFormat.Write(re, _cdex);

        var loader = Substitute.For<ILoadCatalogService>();
        loader.GetColumnarFiles(Arg.Any<string>()).Returns([_cdex]);
        _session = new CatalogSession(loader, Substitute.For<ILogger>());
        _session.LoadAsync("config").GetAwaiter().GetResult();
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { /* best effort */ }
    }

    [Test]
    public async Task Sets_the_description_in_the_session_and_on_disk()
    {
        await _session.SetDescriptionAsync(0, "  Family photos, 2019  ");

        Assert.That(_session.GetCatalogs().Single().Description, Is.EqualTo("Family photos, 2019"));
        Assert.That(File.Exists(_cdex + ".tmp"), Is.False);
        _session.Dispose();
        using var reopened = new ColumnarCatalogReader(_cdex);
        Assert.That(reopened.Description, Is.EqualTo("Family photos, 2019"));
    }

    [Test]
    public async Task Keeps_every_entry_and_the_header_flags()
    {
        int count;
        CatalogHashes hashes;
        using (var before = new ColumnarCatalogReader(_cdex))
        {
            count = before.Count;
            hashes = before.Hashes;
            Assert.That(before.FollowJunctions, Is.True);
        }

        var names = _session.GetChildren(new EntryRefDto(0, 0)).Select(c => c.Name).OrderBy(n => n).ToArray();
        await _session.SetDescriptionAsync(0, "changed");

        Assert.That(_session.GetChildren(new EntryRefDto(0, 0)).Select(c => c.Name).OrderBy(n => n), Is.EqualTo(names));
        _session.Dispose();
        using var after = new ColumnarCatalogReader(_cdex);
        Assert.That(after.Count, Is.EqualTo(count));
        Assert.That(after.Hashes, Is.EqualTo(hashes));
        Assert.That(after.FollowJunctions, Is.True);
    }

    [Test]
    public async Task An_empty_description_clears_it()
    {
        await _session.SetDescriptionAsync(0, "   ");

        Assert.That(_session.GetCatalogs().Single().Description ?? "", Is.Empty);
    }

    [Test]
    public async Task The_new_description_survives_a_rescan()
    {
        await _session.SetDescriptionAsync(0, "kept");

        await _session.RescanAsync(0);

        Assert.That(_session.GetCatalogs().Single().Description, Is.EqualTo("kept"));
    }

    [Test]
    public void A_description_over_the_limit_is_refused()
    {
        var tooLong = new string('x', ICatalogSession.MaxDescriptionLength + 1);

        Assert.ThrowsAsync<ArgumentException>(() => _session.SetDescriptionAsync(0, tooLong));
        Assert.That(_session.GetCatalogs().Single().Description, Is.EqualTo("first"));
    }

    [Test]
    public void An_unknown_catalog_is_refused()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _session.SetDescriptionAsync(7, "x"));
    }
}

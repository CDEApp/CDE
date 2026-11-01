using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Output;
using NSubstitute;
using NUnit.Framework;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Output;

/// <summary><c>hash --json</c> / <c>--csv</c> (#173): a catalog's records are written only once that catalog is saved.</summary>
[TestFixture]
[NonParallelizable]
public class HashOutputTests
{
    private string _dir;

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("cde-hash-output").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    private static IConfiguration Config()
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(100);
        config.HashFirstPassSize.Returns(1024);
        config.DegreesOfParallelism.Returns(1);
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        return config;
    }

    private static Duplication NewDuplication() =>
        new(Substitute.For<ILogger>(), Config(), Substitute.For<IApplicationDiagnostics>(), new OperationCancellation());

    // folder/ two files with the same content, so both are hashed.
    private string Catalog(string folder)
    {
        var data = Path.Combine(_dir, folder);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "a.txt"), "hello " + folder);
        File.WriteAllText(Path.Combine(data, "b.txt"), "hello " + folder);
        var root = new RootEntry(Config());
        root.PopulateRoot(data);
        root.SortAllChildrenByPath();
        root.SetSummaryFields();
        var cdex = Path.Combine(_dir, folder + ".cdex");
        ColumnarFormat.Write(root, cdex);
        return cdex;
    }

    private static string[] Catalogs(StringWriter text) =>
        text.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement.GetProperty("catalog").GetString()!).ToArray();

    [Test]
    public async Task Records_are_written_for_every_catalog_that_saved()
    {
        var one = Catalog("one");
        var two = Catalog("two");
        var text = new StringWriter();

        await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [one, two], null,
            RecordWriter.Create(OutputFormat.Json, text, EntryRecords.HashColumns));

        Assert.That(Catalogs(text), Is.EqualTo(new[] { one, one, two, two }));
    }

    [Test]
    public async Task A_catalog_whose_save_fails_emits_none_of_its_records_and_the_others_still_do()
    {
        var one = Catalog("one");
        var two = Catalog("two");
        Directory.CreateDirectory(one + ".tmp"); // the save of "one" can't create its temp file
        var text = new StringWriter();

        IOException failure = null;
        try
        {
            await ColumnarCatalogHashing.HashAndSave(NewDuplication(), [one, two], null,
                RecordWriter.Create(OutputFormat.Json, text, EntryRecords.HashColumns));
        }
        catch (IOException ex)
        {
            failure = ex;
        }

        Assert.That(failure, Is.Not.Null, "the failed save is still reported");
        Assert.That(Catalogs(text), Is.EqualTo(new[] { two, two }));
    }
}

using System.IO;
using System.Text.Json.Nodes;
using cdeApi;
using NUnit.Framework;

namespace cdeApiTest;

[TestFixture]
public class UiStateStoreTests
{
    private string _dir = null!;
    private string FilePath => Path.Combine(_dir, "ui-state.json");

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("cde-ui-state-test").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    [Test]
    public void GetMergedJson_WhenNoFile_ReturnsDefaults()
    {
        var json = JsonNode.Parse(new UiStateStore(FilePath).GetMergedJson())!;

        Assert.That((int)json["catalogColumnCount"]!, Is.EqualTo(13));
        Assert.That((int)json["directoryColumnCount"]!, Is.EqualTo(3));
        Assert.That((int)json["searchResultColumnCount"]!, Is.EqualTo(5));
        Assert.That(json["searchHistory"], Is.InstanceOf<JsonArray>());
    }

    [Test]
    public void Constructor_CreatesMissingDirectory()
    {
        var path = Path.Combine(_dir, "nested", "ui-state.json");

        _ = new UiStateStore(path);

        Assert.That(Directory.Exists(Path.Combine(_dir, "nested")), Is.True);
    }

    [Test]
    public void Save_ThenGetMergedJson_UserValuesWinOverDefaults()
    {
        var store = new UiStateStore(FilePath);

        store.Save("""{"directoryColumnCount": 7, "theme": "dark"}""");
        var json = JsonNode.Parse(store.GetMergedJson())!;

        Assert.That((int)json["directoryColumnCount"]!, Is.EqualTo(7));
        Assert.That((string)json["theme"]!, Is.EqualTo("dark"));
        Assert.That((int)json["catalogColumnCount"]!, Is.EqualTo(13));
    }

    [Test]
    public void GetMergedJson_MergesNestedObjectsRecursively()
    {
        File.WriteAllText(FilePath, """{"searchHistory": ["a"], "extra": {"x": 1}}""");
        var json = JsonNode.Parse(new UiStateStore(FilePath).GetMergedJson())!;

        Assert.That(json["searchHistory"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That((int)json["extra"]!["x"]!, Is.EqualTo(1));
    }

    [Test]
    public void GetMergedJson_BlankOrNonObjectFile_FallsBackToDefaults()
    {
        var store = new UiStateStore(FilePath);

        File.WriteAllText(FilePath, "   ");
        Assert.That((int)JsonNode.Parse(store.GetMergedJson())!["catalogColumnCount"]!, Is.EqualTo(13));

        File.WriteAllText(FilePath, "[1,2]");
        Assert.That((int)JsonNode.Parse(store.GetMergedJson())!["catalogColumnCount"]!, Is.EqualTo(13));
    }

    [Test]
    public void GetMergedJson_NullUserValue_OverridesDefault()
    {
        File.WriteAllText(FilePath, """{"catalogColumnCount": null}""");

        var json = JsonNode.Parse(new UiStateStore(FilePath).GetMergedJson())!;

        Assert.That(json["catalogColumnCount"], Is.Null);
    }

    [Test]
    public void Save_InvalidJson_ThrowsAndWritesNothing()
    {
        var store = new UiStateStore(FilePath);

        Assert.That(() => store.Save("{not json"), Throws.Exception);
        Assert.That(File.Exists(FilePath), Is.False);
    }
}

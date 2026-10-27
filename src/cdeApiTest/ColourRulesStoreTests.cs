using System.IO;
using System.Linq;
using cdeApi;
using NUnit.Framework;

namespace cdeApiTest;

[TestFixture]
public class ColourRulesStoreTests
{
    private string _dir = null!;
    private string FilePath => Path.Combine(_dir, "colour-rules.txt");

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("cde-colour-rules-test").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    [Test]
    public void Read_WhenFileMissing_CreatesItFromDefaults()
    {
        var store = new ColourRulesStore(FilePath);

        var text = store.Read();

        Assert.That(text, Is.EqualTo(ColourRulesStore.DefaultText));
        Assert.That(File.Exists(FilePath), Is.True);
        Assert.That(File.ReadAllText(FilePath), Is.EqualTo(ColourRulesStore.DefaultText));
    }

    [Test]
    public void Read_WhenFileMissingAndDirectoryMissing_CreatesDirectory()
    {
        var path = Path.Combine(_dir, "nested", "colour-rules.txt");

        new ColourRulesStore(path).Read();

        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void Read_WhenFileExists_ReturnsItVerbatimIncludingInvalidLines()
    {
        File.WriteAllText(FilePath, "(bad red\n\\.sys$ blue");

        Assert.That(new ColourRulesStore(FilePath).Read(), Is.EqualTo("(bad red\n\\.sys$ blue"));
    }

    [Test]
    public void Save_PersistsTextAsTyped()
    {
        var store = new ColourRulesStore(FilePath);

        store.Save("x red\n(oops");

        Assert.That(File.ReadAllText(FilePath), Is.EqualTo("x red\n(oops"));
        Assert.That(store.Read(), Is.EqualTo("x red\n(oops"));
    }

    [Test]
    public void Save_OverExistingFile_ReplacesItAndLeavesNoTempFile()
    {
        File.WriteAllText(FilePath, "old red");
        var store = new ColourRulesStore(FilePath);

        store.Save("new blue");

        Assert.That(File.ReadAllText(FilePath), Is.EqualTo("new blue"));
        Assert.That(Directory.GetFiles(_dir).Select(Path.GetFileName), Is.EqualTo(new[] { "colour-rules.txt" }));
    }

    [Test]
    public void Save_EmptyText_IsKeptAndNotReplacedByDefaults()
    {
        var store = new ColourRulesStore(FilePath);

        store.Save("");

        Assert.That(store.Read(), Is.EqualTo(""));
    }

    [Test]
    public void DefaultText_HasFolderImageArchiveAndTempRulesAndWarning()
    {
        var active = ColourRulesStore.DefaultText
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

        Assert.That(active, Has.Count.EqualTo(4));
        Assert.That(active[0], Does.StartWith("[folder]").And.Contain("var(--dir)").And.Contain("font-weight:bold"));
        Assert.That(active[1], Does.StartWith(@"\.(jpg|png|gif)$").And.EndWith("#2a9d8f"));
        Assert.That(active[2], Does.StartWith(@"\.(zip|").And.Contain("7z").And.EndWith("#e5a34b"));
        Assert.That(active[3], Does.StartWith(@"^~\$|\.tmp$").And.Contain("font-style: italic"));
        Assert.That(ColourRulesStore.DefaultText, Does.Contain(@"# \.sys$"), ".sys stays a commented-out example");
        Assert.That(ColourRulesStore.DefaultText, Does.Contain("(a+)+$"));
    }

    [Test]
    public void DefaultText_ActivePatternsAreValidRegexes()
    {
        // .NET and JavaScript regex syntax agree for these simple patterns; the app compiles them as JS.
        var patterns = ColourRulesStore.DefaultText
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("[folder]"))
            .Select(l => l.Split((char[])null, 2, System.StringSplitOptions.RemoveEmptyEntries)[0]);

        foreach (var p in patterns)
        {
            Assert.DoesNotThrow(() => _ = new System.Text.RegularExpressions.Regex(p), p);
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using cdeAppCore.Formatting;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class SizeFormatterTests
{
    private sealed record SizeCase(long Bytes, string Expected);

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private sealed record SizeCaseFile(List<SizeCase> Cases);

    // The same file drives cde-tauri's toHRString tests, so both frontends format sizes identically.
    private static IEnumerable<TestCaseData> SharedCases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "size-format-cases.json");
        var file = JsonSerializer.Deserialize<SizeCaseFile>(File.ReadAllText(path),
            CaseInsensitive);
        if (file?.Cases is not { Count: > 0 })
        {
            throw new InvalidDataException($"No size cases in {path}");
        }

        foreach (var c in file.Cases)
        {
            yield return new TestCaseData(c.Bytes).Returns(c.Expected).SetArgDisplayNames(c.Bytes.ToString(CultureInfo.InvariantCulture));
        }
    }

    [TestCaseSource(nameof(SharedCases))]
    public string ToHRString_MatchesSharedCases(long bytes)
    {
        return bytes.ToHRString();
    }

    // cdeLib cannot reference cdeAppCore, so the cde --template {size} carries its own copy of this formatting.
    // Running the same shared cases through both keeps the CLI and the GUIs from drifting apart.
    [TestCaseSource(nameof(SharedCases))]
    public string MatchTemplateSize_MatchesSharedCases(long bytes)
    {
        return cdeLib.MatchTemplate.Parse("{size}").Render("catalog", @"C:\a.txt", bytes);
    }

    [Test]
    public void ToHRString_IsCultureInvariant()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            // A value no other test formats, with the cache cleared just before, so a cached
            // invariant string can't make this pass.
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            SizeFormatter.ClearSizeCache();
            Assert.That(1587L.ToHRString(), Is.EqualTo("1.5 KB"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
            SizeFormatter.ClearSizeCache();
        }
    }
}

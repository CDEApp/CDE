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

    private sealed record SizeCaseFile(List<SizeCase> Cases);

    // The same file drives cde-tauri's toHRString tests, so both frontends format sizes identically.
    private static IEnumerable<TestCaseData> SharedCases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "size-format-cases.json");
        var file = JsonSerializer.Deserialize<SizeCaseFile>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        foreach (var c in file!.Cases)
        {
            yield return new TestCaseData(c.Bytes).Returns(c.Expected).SetArgDisplayNames(c.Bytes.ToString(CultureInfo.InvariantCulture));
        }
    }

    [TestCaseSource(nameof(SharedCases))]
    public string ToHRString_MatchesSharedCases(long bytes)
    {
        return bytes.ToHRString();
    }

    [Test]
    public void ToHRString_IsCultureInvariant()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            SizeFormatter.ClearSizeCache();
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.That(1536L.ToHRString(), Is.EqualTo("1.5 KB"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
            SizeFormatter.ClearSizeCache();
        }
    }
}

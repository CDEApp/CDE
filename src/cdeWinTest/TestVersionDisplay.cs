using NUnit.Framework;
using cdeWin;

namespace cdeWinTest;

[TestFixture]
public class TestVersionDisplay
{
    [TestCase("1.7.2-alpha.0.344+ef4a733daca63c8e1f2a3b4c5d6e7f8091a2b3c4", "1.7.2-alpha.0.344+ef4a733")]
    [TestCase("1.7.2+ef4a733", "1.7.2+ef4a733")]
    [TestCase("1.7.2+abc", "1.7.2+abc")]
    [TestCase("1.7.2", "1.7.2")]
    [TestCase("1.7.2+", "1.7.2+")]
    [TestCase("", "")]
    public void ShortenBuildMetadata_TrimsShaToSevenChars(string input, string expected)
    {
        Assert.That(VersionDisplay.ShortenBuildMetadata(input), Is.EqualTo(expected));
    }

    [Test]
    public void ShortenBuildMetadata_Null_ReturnsEmpty()
    {
        Assert.That(VersionDisplay.ShortenBuildMetadata(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void ShortenBuildMetadata_MetadataWithDotSeparatedParts_TrimsOnlyFirstSevenChars()
    {
        Assert.That(VersionDisplay.ShortenBuildMetadata("1.0.0+ef4a733daca.dirty"), Is.EqualTo("1.0.0+ef4a733"));
    }
}

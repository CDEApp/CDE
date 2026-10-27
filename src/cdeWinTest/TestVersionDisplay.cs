using NUnit.Framework;
using cdeWin;

namespace cdeWinTest;

[TestFixture]
public class TestVersionDisplay
{
    private const string Sha = "ef4a733daca63c8e1f2a3b4c5d6e7f8091a2b3c4";

    [TestCase("1.7.2-alpha.0.344+" + Sha, "1.7.2-alpha.0.344+ef4a733")]
    [TestCase("1.7.2+" + Sha + ".dirty", "1.7.2+ef4a733.dirty")]
    [TestCase("1.7.2+build.5." + Sha, "1.7.2+build.5.ef4a733")]
    [TestCase("1.7.2+build.12345678", "1.7.2+build.12345678")]
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
}

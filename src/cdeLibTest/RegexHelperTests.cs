using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest;

[TestFixture]
public class RegexHelperTests
{
    [Test]
    public void ValidPattern_ReturnsEmpty() =>
        Assert.That(RegexHelper.GetRegexErrorMessage(@"^a+b$"), Is.Empty);

    [Test]
    public void InvalidPattern_ReturnsBadRegexMessage() =>
        Assert.That(RegexHelper.GetRegexErrorMessage("(unclosed"), Does.StartWith("Bad Regex:"));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void NullOrBlank_ReturnsBadRegexMessage(string pattern) =>
        Assert.That(RegexHelper.GetRegexErrorMessage(pattern), Does.StartWith("Bad Regex:"));

    [Test]
    public void CatastrophicPattern_IsStillValidAndReturnsPromptly() =>
        Assert.That(RegexHelper.GetRegexErrorMessage(@"^(a+)+$"), Is.Empty);
}

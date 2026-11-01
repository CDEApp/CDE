using cde.ScanProgress;
using NUnit.Framework;

namespace cdeLibTest;

[TestFixture]
public class ScanExcludeWarningsTests
{
    [Test]
    public void Lines_InvalidPattern_NamesThePattern()
    {
        var lines = ScanExcludeWarnings.Lines(["node_modules", "/abs/ok", "sub/dir", "  ", null, "../x"], isWindows: false);

        Assert.That(lines, Has.Count.EqualTo(2));
        Assert.That(lines[0], Does.Contain("\"sub/dir\"").And.Contain("--exclude"));
        Assert.That(lines[1], Does.Contain("../x"));
    }

    [Test]
    public void Lines_AllValidOrNone_IsEmpty()
    {
        Assert.That(ScanExcludeWarnings.Lines(["node_modules", "/abs"], isWindows: false), Is.Empty);
        Assert.That(ScanExcludeWarnings.Lines(null, isWindows: true), Is.Empty);
    }

    [Test]
    public void Lines_WindowsAbsolutePath_IsValid()
    {
        Assert.That(ScanExcludeWarnings.Lines([@"C:\temp\x"], isWindows: true), Is.Empty);
        Assert.That(ScanExcludeWarnings.Lines([@"temp\x"], isWindows: true), Has.Count.EqualTo(1));
    }
}

using System;
using System.IO;
using cdeAppCore.Session;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class CatalogNamingTests
{
    [Test]
    public void Default_name_for_a_volume_root_has_hint_and_volume_and_no_extension()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        var name = CatalogNaming.DefaultFileName(root);
        Assert.That(name, Does.Not.EndWith(".cde").And.Not.EndWith(".cdex"));
        Assert.That(name, Does.StartWith(root.Substring(0, 1).ToUpperInvariant() + "-").Or.Not.Empty);
        // A volume root is "{hint}-{volume}": no path segment is appended.
        Assert.That(name, Is.Not.Contains(Path.GetFileName(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar))));
    }

    [Test]
    public void Default_name_for_a_sub_folder_appends_the_safe_path_and_extends_the_root_name()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        var sub = Path.Combine(Path.GetTempPath(), "Photos");
        var rootName = CatalogNaming.DefaultFileName(root);
        var name = CatalogNaming.DefaultFileName(sub);
        Assert.That(name, Does.StartWith(rootName + "-"));
        Assert.That(name, Does.EndWith("Photos"));
        Assert.That(name, Is.Not.Contains(Path.DirectorySeparatorChar.ToString()));
    }

    [Test]
    public void Default_name_ignores_a_trailing_separator()
    {
        var sub = Path.Combine(Path.GetTempPath(), "Photos");
        Assert.That(CatalogNaming.DefaultFileName(sub + Path.DirectorySeparatorChar),
            Is.EqualTo(CatalogNaming.DefaultFileName(sub)));
    }

    [Test]
    public void Default_name_for_a_unc_path_uses_server_share_and_path()
    {
        Assume.That(OperatingSystem.IsWindows(), "UNC paths are a Windows concept");
        var name = CatalogNaming.DefaultFileName(@"\\toothless\c$\users");
        Assert.That(name, Does.Contain("toothless"));
        Assert.That(name, Does.Contain("users"));
        Assert.That(name, Does.Not.Contain("\\").And.Not.Contain(".cde"));
    }

    [Test]
    public void Default_name_of_garbage_is_null()
    {
        Assert.That(CatalogNaming.DefaultFileName(""), Is.Null);
        Assert.That(CatalogNaming.DefaultFileName(null), Is.Null);
        Assert.That(CatalogNaming.DefaultFileName("bad\0path"), Is.Null);
    }

    [TestCase("D-Photos")]
    [TestCase("my catalog 2024")]
    [TestCase("a.b")]
    public void Good_names_are_valid(string name) => Assert.That(CatalogNaming.ValidateFileName(name), Is.Null);

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("a\\b")]
    [TestCase("a/b")]
    [TestCase("a:b")]
    [TestCase("a*b")]
    [TestCase("a?b")]
    [TestCase("a\"b")]
    [TestCase("a<b")]
    [TestCase("a|b")]
    [TestCase("name.")]
    [TestCase("name ")]
    [TestCase("CON")]
    [TestCase("nul.txt")]
    public void Bad_names_are_rejected_with_a_reason(string name) =>
        Assert.That(CatalogNaming.ValidateFileName(name), Is.Not.Null.And.Not.Empty);

    [Test]
    public void Overlong_names_are_rejected() =>
        Assert.That(CatalogNaming.ValidateFileName(new string('a', CatalogNaming.MaxNameLength + 1)), Is.Not.Null);

    [TestCase("D-Photos", "D-Photos")]
    [TestCase("  D-Photos  ", "D-Photos")]
    [TestCase("D-Photos.cdex", "D-Photos")]
    [TestCase("D-Photos.CDEX", "D-Photos")]
    [TestCase(null, "")]
    public void Normalize_trims_and_drops_a_typed_extension(string input, string expected) =>
        Assert.That(CatalogNaming.NormalizeFileName(input), Is.EqualTo(expected));

    [Test]
    public void Same_root_ignores_case_and_trailing_separator()
    {
        var p = Path.Combine(Path.GetTempPath(), "Photos");
        Assert.That(CatalogNaming.SameRoot(p, p.ToUpperInvariant() + Path.DirectorySeparatorChar), Is.True);
        Assert.That(CatalogNaming.SameRoot(p, p + "2"), Is.False);
        Assert.That(CatalogNaming.SameRoot(p, null), Is.False);
    }
}

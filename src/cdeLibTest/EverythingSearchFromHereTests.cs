using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// The queries cde-tauri's "Search from here" builds (<c>searchFromHereQuery</c> in everythingSearch.ts) must read as a
/// literal folder scope under the real parser. The query strings are copied from that function's tests.
/// </summary>
[TestFixture]
public class EverythingSearchFromHereTests
{
    private static bool Matches(string query, string fullPath)
    {
        var name = fullPath[(fullPath.LastIndexOfAny(['/', '\\']) + 1)..];
        return EverythingQuery.Parse(query).Matches(name, fullPath, defaultMatchesPath: false);
    }

    [TestCase("path:/home/jo/ ", "/home/jo/a.txt", true)]
    [TestCase("path:/home/jo/ ", "/home/jo/sub/a.txt", true)]
    [TestCase("path:/home/jo/ ", "/home/joe/a.txt", false)]
    [TestCase("path:\"C:\\Users\\jo\\\" ", "C:\\Users\\jo\\a.txt", true)]
    [TestCase("path:\"C:\\Users\\jo\\\" ", "C:\\Users\\joe\\a.txt", false)]
    [TestCase("path:\"C:\\My Docs\\\" ", "C:\\My Docs\\a.txt", true)]
    [TestCase("path:\\\\srv\\share\\d\\ ", "\\\\srv\\share\\d\\a.txt", true)]
    public void A_folder_scope_matches_only_what_is_inside_it(string scope, string path, bool expected)
    {
        Assert.That(Matches(scope, path), Is.EqualTo(expected));
    }

    [TestCase("/data/what?", "/data/what?/a.txt", true)]
    [TestCase("/data/what?", "/data/whatX/a.txt", false)]
    [TestCase("/data/a*b", "/data/a*b/a.txt", true)]
    [TestCase("/data/a*b", "/data/axxb/a.txt", false)]
    [TestCase("/data/say\"hi\"", "/data/say\"hi\"/a.txt", true)]
    [TestCase("/data/a\\b", "/data/a\\b/a.txt", true)]
    public void A_name_with_a_wildcard_quote_or_backslash_is_taken_literally(string folder, string path, bool expected)
    {
        // The three scopes below are what searchFromHereQuery returns for these folders.
        var scope = folder switch
        {
            "/data/what?" => "path:regex:\"^/data/what\\?/\" ",
            "/data/a*b" => "path:regex:\"^/data/a\\*b/\" ",
            "/data/say\"hi\"" => "path:regex:\"^/data/say\\x22hi\\x22/\" ",
            _ => "path:/data/a\\b/ ",
        };
        Assert.That(Matches(scope, path), Is.EqualTo(expected));
    }

    [Test]
    public void The_catalog_term_forms_parse()
    {
        Assert.DoesNotThrow(() => EverythingQuery.Parse("catalog:media path:/home/jo/ txt"));
        Assert.DoesNotThrow(() => EverythingQuery.Parse("catalog:\"my cats\" path:\"C:\\My Docs\\\" txt"));
        Assert.DoesNotThrow(() => EverythingQuery.Parse("catalog:regex:\"we\\?rd\" path:/d/ txt"));
    }
}

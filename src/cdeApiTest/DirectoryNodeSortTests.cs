using System;
using cdeApi;
using cdeAppCore.Dtos;
using NUnit.Framework;

namespace cdeApiTest;

[TestFixture]
public class DirectoryNodeSortTests
{
    // Deliberately out of every sort order (a file before a directory, names and sizes descending),
    // so any reordering shows up.
    private static DirectoryNodeDto[] Nodes() =>
    [
        new() { Name = "zeta.txt", IsDirectory = false, Size = 30, Modified = new DateTime(2024, 3, 1) },
        new() { Name = "beta", IsDirectory = true, Size = 20, Modified = new DateTime(2024, 2, 1) },
        new() { Name = "alpha.txt", IsDirectory = false, Size = 10, Modified = new DateTime(2024, 1, 1) },
    ];

    // Keys that match no sort arm: unknown, unknown descending, a bare "-" (empty key) and a
    // known key in the wrong case (matching is case-sensitive).
    [TestCase("bogus")]
    [TestCase("-bogus")]
    [TestCase("-")]
    [TestCase("Name")]
    public void Apply_UnrecognisedKey_ReturnsInputInNaturalOrder(string sort)
    {
        var nodes = Nodes();

        var result = DirectoryNodeSort.Apply(nodes, sort);

        Assert.That(result, Is.SameAs(nodes));
        Assert.That(result, Is.EqualTo(Nodes()).Using<DirectoryNodeDto>((a, b) => a.Name == b.Name));
    }
}

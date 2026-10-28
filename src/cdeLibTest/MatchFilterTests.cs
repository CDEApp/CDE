using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// <see cref="MatchFilter"/>: parsing of the <c>--filter</c> text and testing a size against it.
/// </summary>
[TestFixture]
public class MatchFilterTests
{
    private const long KB = 1024;
    private const long MB = 1024 * KB;
    private const long GB = 1024 * MB;
    private const long TB = 1024 * GB;

    private static bool Matches(string filter, long size) => MatchFilter.Parse([filter]).Matches(size);

    // ---- operators ----

    [TestCase("{size:<4MB}", 4 * MB - 1, true)]
    [TestCase("{size:<4MB}", 4 * MB, false)]
    [TestCase("{size:<4MB}", 4 * MB + 1, false)]
    [TestCase("{size:<=4MB}", 4 * MB, true)]
    [TestCase("{size:<=4MB}", 4 * MB + 1, false)]
    [TestCase("{size:>10KB}", 10 * KB, false)]
    [TestCase("{size:>10KB}", 10 * KB + 1, true)]
    [TestCase("{size:>=10KB}", 10 * KB, true)]
    [TestCase("{size:>=10KB}", 10 * KB - 1, false)]
    [TestCase("{size:=1KB}", KB, true)]
    [TestCase("{size:=1KB}", KB + 1, false)]
    [TestCase("{size:!=1KB}", KB, false)]
    [TestCase("{size:!=1KB}", KB + 1, true)]
    public void Operator_compares_the_size_with_the_value(string filter, long size, bool expected)
    {
        Assert.That(Matches(filter, size), Is.EqualTo(expected));
    }

    [TestCase(KB, true)]
    [TestCase(KB - 1, false)]
    public void A_value_with_no_operator_means_equals(long size, bool expected)
    {
        Assert.That(Matches("{size:1KB}", size), Is.EqualTo(expected));
    }

    // ---- values ----

    [TestCase("{size:>1KB}", 1024, false)] // 1 KB is 1024 bytes, not 1000
    [TestCase("{size:>1KB}", 1025, true)]
    [TestCase("{size:=1B}", 1, true)]
    [TestCase("{size:=1MB}", 1048576, true)]
    [TestCase("{size:=1GB}", 1073741824, true)]
    [TestCase("{size:=2TB}", 2199023255552, true)]
    public void Units_step_by_1024(string filter, long size, bool expected)
    {
        Assert.That(Matches(filter, size), Is.EqualTo(expected));
    }

    [Test]
    public void A_value_with_no_unit_is_in_bytes()
    {
        Assert.That(Matches("{size:>100}", 101), Is.True);
        Assert.That(Matches("{size:>100}", 100), Is.False);
    }

    [Test]
    public void A_value_can_have_decimals()
    {
        Assert.That(Matches("{size:=1.5KB}", 1536), Is.True);
        Assert.That(Matches("{size:>=0.5MB}", 512 * KB), Is.True);
        Assert.That(Matches("{size:>=0.5MB}", 512 * KB - 1), Is.False);
    }

    [Test]
    public void Names_and_units_are_not_case_sensitive()
    {
        Assert.That(Matches("{SIZE:>1kb}", 2 * KB), Is.True);
        Assert.That(Matches("{Size:>1Kb}", KB), Is.False);
        Assert.That(Matches("{size:>1b}", 2), Is.True);
    }

    [Test]
    public void Spaces_around_the_operator_and_the_unit_are_allowed()
    {
        Assert.That(Matches("{size: > 4 MB }", 5 * MB), Is.True);
        Assert.That(Matches("{size: >= 4 MB}", 4 * MB), Is.True);
    }

    [Test]
    public void Zero_size_is_comparable()
    {
        Assert.That(Matches("{size:=0}", 0), Is.True);
        Assert.That(Matches("{size:>0}", 0), Is.False);
    }

    [Test]
    public void The_largest_sizes_compare_without_overflow()
    {
        Assert.That(Matches("{size:<1TB}", long.MaxValue), Is.False);
        Assert.That(Matches("{size:>1TB}", long.MaxValue), Is.True);
        Assert.That(Matches("{size:>=9223372036854775807}", long.MaxValue), Is.True);
    }

    // ---- several filters ----

    [Test]
    public void Several_filters_must_all_match()
    {
        var range = MatchFilter.Parse(["{size:>1KB}", "{size:<4MB}"]);

        Assert.That(range.Matches(KB), Is.False);
        Assert.That(range.Matches(KB + 1), Is.True);
        Assert.That(range.Matches(4 * MB - 1), Is.True);
        Assert.That(range.Matches(4 * MB), Is.False);
    }

    [Test]
    public void A_filter_that_cannot_be_met_matches_nothing()
    {
        var never = MatchFilter.Parse(["{size:>4MB}", "{size:<1KB}"]);

        Assert.That(never.Matches(0), Is.False);
        Assert.That(never.Matches(5 * MB), Is.False);
    }

    // ---- bad filters ----

    [Test]
    public void An_unknown_filter_throws_and_lists_the_valid_ones()
    {
        var ex = Assert.Throws<FormatException>(() => MatchFilter.Parse(["{owner:>1}"]));

        Assert.That(ex!.Message, Does.Contain("owner"));
        Assert.That(ex.Message, Does.Contain("{size}"));
    }

    [Test]
    public void An_unknown_unit_throws_and_lists_the_valid_ones()
    {
        var ex = Assert.Throws<FormatException>(() => MatchFilter.Parse(["{size:>1XB}"]));

        Assert.That(ex!.Message, Does.Contain("XB"));
        Assert.That(ex.Message, Does.Contain("KB"));
        Assert.That(ex.Message, Does.Contain("TB"));
    }

    [TestCase("size:>1KB")] // no braces
    [TestCase("{size:>1KB")] // unclosed
    [TestCase("{size}")] // no colon
    [TestCase("{size:}")] // nothing to compare with
    [TestCase("{size:>}")] // no number
    [TestCase("{size:>MB}")] // no number
    [TestCase("{size:~1KB}")] // not an operator
    [TestCase("{size:>1KB 2}")] // trailing text
    [TestCase("{size:>1.KB}")] // half a decimal
    [TestCase("{size:>-1}")] // sizes are not negative
    [TestCase("{size:>99999999999999999999TB}")] // too large to compare
    [TestCase("")]
    public void A_malformed_filter_throws(string filter)
    {
        Assert.Throws<FormatException>(() => MatchFilter.Parse([filter]));
    }

    [Test]
    public void A_bad_filter_among_good_ones_throws()
    {
        Assert.Throws<FormatException>(() => MatchFilter.Parse(["{size:>1KB}", "{size:>1XB}"]));
    }

    [Test]
    public void No_filters_is_an_error()
    {
        Assert.Throws<ArgumentException>(() => MatchFilter.Parse([]));
        Assert.Throws<ArgumentNullException>(() => MatchFilter.Parse(null));
    }
}

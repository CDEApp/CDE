using System;
using System.Text;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// The Everything functions <c>size:</c>, <c>date-modified:</c> (<c>dm:</c>) and the <c>regex:</c> modifier of
/// <see cref="EverythingQuery"/>. The clock is fixed at Friday 2026-07-10 12:00:00 so relative dates are exact.
/// </summary>
[TestFixture]
public class EverythingFunctionTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0);

    private sealed class FixedClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private static EverythingQuery Q(string query) => EverythingQuery.Parse(query, new FixedClock());

    private const long Kb = 1024;
    private const long Mb = 1024 * Kb;

    private static bool Size(string query, long size) => Q(query).Matches("a.bin", size: size);

    private static bool Date(string query, DateTime modified) => Q(query).Matches("a.bin", modifiedTicks: modified.Ticks);

    // ---- size ----

    [TestCase("size:1kb", 1023, false)]
    [TestCase("size:1kb", 1024, true)]
    [TestCase("size:1kb", 2047, true)]
    [TestCase("size:1kb", 2048, false)]
    [TestCase("size:==1kb", 1024, true)]
    [TestCase("size:==1kb", 1025, false)]
    [TestCase("size:=1kb", 1024, true)]
    [TestCase("size:=1kb", 1025, false)]
    [TestCase("size:!=1kb", 1024, false)]
    [TestCase("size:!=1kb", 1500, true)]
    [TestCase("size:!1kb", 1500, false)]
    [TestCase("size:!1kb", 2048, true)]
    [TestCase("size:<10mb", 10 * Mb - 1, true)]
    [TestCase("size:<10mb", 10 * Mb, false)]
    [TestCase("size:<=10mb", 10 * Mb, true)]
    [TestCase("size:<=10mb", 10 * Mb + 1, false)]
    [TestCase("size:>10mb", 10 * Mb, false)]
    [TestCase("size:>10mb", 10 * Mb + 1, true)]
    [TestCase("size:>=10mb", 10 * Mb, true)]
    [TestCase("size:>=10mb", 10 * Mb - 1, false)]
    [TestCase("size:2mb..3mb", 2 * Mb - 1, false)]
    [TestCase("size:2mb..3mb", 2 * Mb, true)]
    [TestCase("size:2mb..3mb", 3 * Mb, true)]
    [TestCase("size:2mb..3mb", 3 * Mb + 1, false)]
    [TestCase("size:2mb-3mb", 2 * Mb, true)]
    [TestCase("size:2mb-3mb", 3 * Mb + 1, false)]
    [TestCase("size:3mb..2mb", 2 * Mb + 5, true)]
    [TestCase("size:100", 100, true)]
    [TestCase("size:100", 101, false)]
    [TestCase("size:100b", 100, true)]
    [TestCase("size:1.5kb", 1536, true)]
    [TestCase("size:1.5kb", 1536 + 101, true)]
    [TestCase("size:1.5kb", 1536 + 102, false)]
    [TestCase("SIZE:1KB", 1024, true)]
    [TestCase("size:1MiB", Mb, true)]
    [TestCase("size:1gb", 1L << 30, true)]
    [TestCase("size:>1tb", (1L << 40) + 1, true)]
    public void Size_forms(string query, long size, bool expected) =>
        Assert.That(Size(query, size), Is.EqualTo(expected));

    [Test]
    public void Size_zero_matches_empty_files()
    {
        Assert.That(Size("size:0", 0), Is.True);
        Assert.That(Size("size:0", 1), Is.False);
        Assert.That(Size("size:>0", 0), Is.False);
    }

    [TestCase("size:<=1mb a", "a.bin", 100, true)]
    [TestCase("size:<=1mb a", "b.bin", 100, false)]
    [TestCase("size:>1mb | a", "b.bin", 100, false)]
    [TestCase("size:>1mb | a", "a.bin", 100, true)]
    [TestCase("size:>1mb | a", "b.bin", 2 * Mb, true)]
    [TestCase("!size:>1mb", "a.bin", 100, true)]
    [TestCase("!size:>1mb", "a.bin", 2 * Mb, false)]
    [TestCase("<size:>1mb> a", "a.bin", 2 * Mb, true)]
    [TestCase("<size:>1mb | size:<10> a", "a.bin", 5, true)]
    [TestCase("<size:>=1mb>", "a.bin", Mb, true)]
    public void Size_combines_with_the_other_operators(string query, string name, long size, bool expected) =>
        Assert.That(Q(query).Matches(name, size: size), Is.EqualTo(expected));

    [Test]
    public void Dashes_in_a_function_name_are_optional_and_dm_is_an_alias()
    {
        var modified = new DateTime(2026, 7, 10, 3, 0, 0);
        Assert.That(Date("date-modified:today", modified), Is.True);
        Assert.That(Date("datemodified:today", modified), Is.True);
        Assert.That(Date("dm:today", modified), Is.True);
        Assert.That(Date("DM:TODAY", modified), Is.True);
    }

    // ---- date-modified ----

    [TestCase("dm:today", "2026-07-10T00:00:00", true)]
    [TestCase("dm:today", "2026-07-10T23:59:59", true)]
    [TestCase("dm:today", "2026-07-09T23:59:59", false)]
    [TestCase("dm:today", "2026-07-11T00:00:00", false)]
    [TestCase("dm:yesterday", "2026-07-09T08:00:00", true)]
    [TestCase("dm:yesterday", "2026-07-10T08:00:00", false)]
    [TestCase("dm:tomorrow", "2026-07-11T08:00:00", true)]
    [TestCase("dm:2026", "2026-01-01T00:00:00", true)]
    [TestCase("dm:2026", "2026-12-31T23:59:59", true)]
    [TestCase("dm:2026", "2025-12-31T23:59:59", false)]
    [TestCase("dm:2026", "2027-01-01T00:00:00", false)]
    [TestCase("dm:2026-07", "2026-07-31T23:00:00", true)]
    [TestCase("dm:2026-07", "2026-08-01T00:00:00", false)]
    [TestCase("dm:2026-07-02", "2026-07-02T13:00:00", true)]
    [TestCase("dm:2026-07-02", "2026-07-03T00:00:00", false)]
    [TestCase("dm:2026-07-02T10", "2026-07-02T10:59:59", true)]
    [TestCase("dm:2026-07-02T10", "2026-07-02T11:00:00", false)]
    [TestCase("dm:2026-07-02T10:30", "2026-07-02T10:30:45", true)]
    [TestCase("dm:2026-07-02T10:30", "2026-07-02T10:31:00", false)]
    [TestCase("dm:2026-07-02T10:30:15", "2026-07-02T10:30:15", true)]
    [TestCase("dm:2026-07-02T10:30:15", "2026-07-02T10:30:16", false)]
    [TestCase("dm:2026-07-02T10:30:15.250", "2026-07-02T10:30:15.250", true)]
    [TestCase("dm:2026-07-02T10:30:15.250", "2026-07-02T10:30:15.251", false)]
    [TestCase("dm:2026-07-02..2026-07-03", "2026-07-02T00:00:00", true)]
    [TestCase("dm:2026-07-02..2026-07-03", "2026-07-03T23:59:59", true)]
    [TestCase("dm:2026-07-02..2026-07-03", "2026-07-04T00:00:00", false)]
    [TestCase("dm:2026-07-02..2026-07-03", "2026-07-01T23:59:59", false)]
    [TestCase("dm:>2026-07-02", "2026-07-02T23:59:59", false)]
    [TestCase("dm:>2026-07-02", "2026-07-03T00:00:00", true)]
    [TestCase("dm:>=2026-07-02", "2026-07-02T00:00:00", true)]
    [TestCase("dm:>=2026-07-02", "2026-07-01T23:59:59", false)]
    [TestCase("dm:<2026-07-02", "2026-07-01T23:59:59", true)]
    [TestCase("dm:<2026-07-02", "2026-07-02T00:00:00", false)]
    [TestCase("dm:<=2026-07-02", "2026-07-02T23:59:59", true)]
    [TestCase("dm:<=2026-07-02", "2026-07-03T00:00:00", false)]
    [TestCase("dm:==2026-07-02", "2026-07-02T00:00:00", true)]
    [TestCase("dm:==2026-07-02", "2026-07-02T00:00:01", false)]
    [TestCase("dm:!=2026-07-02", "2026-07-02T00:00:00", false)]
    [TestCase("dm:!=2026-07-02", "2026-07-02T00:00:01", true)]
    [TestCase("dm:!2026-07-02", "2026-07-02T10:00:00", false)]
    [TestCase("dm:!2026-07-02", "2026-07-03T10:00:00", true)]
    public void Date_forms(string query, string modified, bool expected) =>
        Assert.That(Date(query, DateTime.Parse(modified)), Is.EqualTo(expected));

    [TestCase("dm:10mins", "2026-07-10T11:50:00", true)]
    [TestCase("dm:10mins", "2026-07-10T11:49:59", false)]
    [TestCase("dm:10min", "2026-07-10T11:55:00", true)]
    [TestCase("dm:30secs", "2026-07-10T11:59:40", true)]
    [TestCase("dm:30secs", "2026-07-10T11:59:20", false)]
    [TestCase("dm:2hours", "2026-07-10T10:00:00", true)]
    [TestCase("dm:2hours", "2026-07-10T09:59:59", false)]
    [TestCase("dm:2days", "2026-07-08T12:00:00", true)]
    [TestCase("dm:2days", "2026-07-08T11:59:59", false)]
    [TestCase("dm:3weeks", "2026-06-19T12:00:00", true)]
    [TestCase("dm:3weeks", "2026-06-19T11:59:59", false)]
    [TestCase("dm:4months", "2026-03-10T12:00:00", true)]
    [TestCase("dm:4months", "2026-03-10T11:59:59", false)]
    [TestCase("dm:1year", "2025-07-10T12:00:00", true)]
    [TestCase("dm:1year", "2025-07-10T11:59:59", false)]
    [TestCase("dm:>2days", "2026-07-09T00:00:00", true)]
    [TestCase("dm:>2days", "2026-07-08T11:00:00", false)]
    [TestCase("dm:<2days", "2026-07-08T11:00:00", true)]
    [TestCase("dm:<2days", "2026-07-09T00:00:00", false)]
    [TestCase("dm:3days..1day", "2026-07-08T00:00:00", true)]
    [TestCase("dm:3days..1day", "2026-07-06T00:00:00", false)]
    public void Relative_dates_count_back_from_now(string query, string modified, bool expected) =>
        Assert.That(Date(query, DateTime.Parse(modified)), Is.EqualTo(expected));

    [Test]
    public void Date_and_size_terms_work_on_the_same_entry()
    {
        var query = Q("size:>1kb dm:2026-07 .bin");
        Assert.That(query.Matches("a.bin", false, 2 * Kb, new DateTime(2026, 7, 5).Ticks), Is.True);
        Assert.That(query.Matches("a.bin", false, 2 * Kb, new DateTime(2026, 8, 5).Ticks), Is.False);
        Assert.That(query.Matches("a.bin", false, 10, new DateTime(2026, 7, 5).Ticks), Is.False);
    }

    [Test]
    public void UsesProperties_is_true_only_when_a_size_or_date_term_is_present()
    {
        Assert.That(Q("abc").UsesProperties, Is.False);
        Assert.That(Q("").UsesProperties, Is.False);
        Assert.That(Q("file: path:abc").UsesProperties, Is.False);
        Assert.That(Q("size:>1").UsesProperties, Is.True);
        Assert.That(Q("abc | dm:today").UsesProperties, Is.True);
        Assert.That(Q("!<abc size:1>").UsesProperties, Is.True);
    }

    [Test]
    public void A_property_term_reads_no_text()
    {
        Assert.That(Q("size:>1").TextsRead(defaultMatchesPath: true), Is.EqualTo(EverythingTexts.Name));
        Assert.That(Q("size:>1 path:x").TextsRead(defaultMatchesPath: false), Is.EqualTo(EverythingTexts.Path));
    }

    [Test]
    public void Entry_overloads_read_size_and_modified_from_the_entry()
    {
        var entry = new Utf8Entry("a.bin", "c:\\a.bin", size: 5 * Mb, modifiedTicks: new DateTime(2026, 7, 10, 9, 0, 0).Ticks);
        Assert.That(Q("size:>1mb dm:today").Matches(entry, defaultMatchesPath: false), Is.True);
        Assert.That(Q("size:>10mb").Matches(entry, defaultMatchesPath: false), Is.False);
        Assert.That(Q("size:>1mb dm:yesterday").Matches(entry, defaultMatchesPath: false), Is.False);
    }

    [Test]
    public void The_bytes_and_chars_overloads_agree()
    {
        var query = Q("size:>1kb dm:today a");
        var modified = new DateTime(2026, 7, 10, 1, 0, 0).Ticks;
        Assert.That(query.Matches(Encoding.UTF8.GetBytes("a.bin"), false, 2 * Kb, modified), Is.True);
        Assert.That(query.Matches("a.bin".AsSpan(), false, 2 * Kb, modified), Is.True);
        Assert.That(query.Matches(Encoding.UTF8.GetBytes("a.bin"), false, 1, modified), Is.False);
    }

    // ---- errors ----

    [TestCase("size:", 0)]
    [TestCase("a size:", 2)]
    [TestCase("size:abc", 0)]
    [TestCase("size:10xb", 0)]
    [TestCase("size:unknown", 0)]
    [TestCase("size:>", 0)]
    [TestCase("size:1..", 0)]
    [TestCase("size:99999999999999999999tb", 0)]
    [TestCase("dm:yesterdayish", 0)]
    [TestCase("dm:2026-13", 0)]
    [TestCase("dm:2026-02-30", 0)]
    [TestCase("dm:2026-07-02T25", 0)]
    [TestCase("dm:3", 0)]
    [TestCase("x dm:garbage", 2)]
    [TestCase("path:size:>1", 0)]
    public void A_bad_function_is_an_error_at_its_position(string query, int position)
    {
        var ex = Assert.Throws<EverythingQueryException>(() => Q(query));
        Assert.That(ex!.Position, Is.EqualTo(position), ex.Message);
    }

    // ---- regex: ----

    [TestCase("regex:^\\d{4}", "2026-report.txt", true)]
    [TestCase("regex:^\\d{4}", "report-2026.txt", false)]
    [TestCase("regex:report", "My REPORT.txt", true)]
    [TestCase("regex:\"a|b\"", "xbx", true)]
    [TestCase("regex:\"a|b\"", "xcx", false)]
    [TestCase("regex:\"^a b$\"", "a b", true)]
    [TestCase("regex:\\.txt$", "a.txt", true)]
    [TestCase("regex:\\.txt$", "a.txt.bak", false)]
    [TestCase("regex:a.c", "abc", true)]
    [TestCase("regex:a*c", "aaac", true)]
    [TestCase("!regex:^a", "abc", false)]
    [TestCase("!regex:^a", "bca", true)]
    [TestCase("regex:^a regex:c$", "abc", true)]
    [TestCase("regex:^a regex:c$", "abd", false)]
    [TestCase("regex:^a | regex:c$", "xxc", true)]
    [TestCase("regex:<^a>", "abc", true)]
    [TestCase("regex:\"^caf\u00e9\"", "CAF\u00c9.txt", true)]
    public void Regex_modifier(string query, string text, bool expected)
    {
        Assert.That(Q(query).Matches(text), Is.EqualTo(expected), "chars");
        Assert.That(Q(query).Matches(Encoding.UTF8.GetBytes(text)), Is.EqualTo(expected), "bytes");
    }

    [Test]
    public void Regex_stands_for_a_group_and_a_nearer_modifier_chain_wins()
    {
        Assert.That(Q("regex:\"<a>\"").Matches("<a>"), Is.True);
        // path: nearest the term decides the text; regex: applies to the term in either order.
        var entry = new Utf8Entry("report.txt", "c:\\2026\\report.txt");
        Assert.That(Q("path:regex:^c:\\\\2026").Matches(entry, defaultMatchesPath: false), Is.True);
        Assert.That(Q("regex:path:^c:\\\\2026").Matches(entry, defaultMatchesPath: false), Is.True);
        Assert.That(Q("regex:^c:").Matches(entry, defaultMatchesPath: false), Is.False);
        Assert.That(Q("regex:no-path:path:^c:").Matches(entry, defaultMatchesPath: false), Is.True);
        Assert.That(Q("path:no-path:regex:^c:").Matches(entry, defaultMatchesPath: false), Is.False);
    }

    [Test]
    public void Regex_applies_to_every_term_in_a_group_and_the_innermost_modifier_still_wins()
    {
        var entry = new Utf8Entry("report.txt", "c:\\2026\\report.txt");
        Assert.That(Q("regex:\"^rep\" regex:\"txt$\"").Matches(entry, defaultMatchesPath: false), Is.True);
        Assert.That(Q("path:<regex:^c: no-path:regex:^rep>").Matches(entry, defaultMatchesPath: false), Is.True);
    }

    [Test]
    public void An_invalid_regex_is_an_error_at_its_term()
    {
        var ex = Assert.Throws<EverythingQueryException>(() => Q("a regex:(["));
        Assert.That(ex!.Position, Is.EqualTo(8));
        Assert.That(ex.Message, Does.Contain("regular expression"));
    }

    [Test]
    public void A_regex_must_be_attached_to_its_term()
    {
        var ex = Assert.Throws<EverythingQueryException>(() => Q("regex: abc"));
        Assert.That(ex!.Position, Is.EqualTo(0));
        Assert.Throws<EverythingQueryException>(() => Q("a regex:"));
    }

    [Test]
    public void A_regex_that_times_out_repeatedly_is_reported_as_exhausted()
    {
        var query = Q("regex:\"^(a+)+$\"");
        Assert.That(query.RegexExhausted, Is.False);
        var text = new string('a', 40) + "!";
        for (var i = 0; i < 5 && !query.RegexExhausted; i++) query.Matches(text);
        Assert.That(query.RegexExhausted, Is.True);
        Assert.That(query.Matches("anything"), Is.False);
    }

    [Test]
    public void Wildcards_and_plain_terms_are_unchanged()
    {
        Assert.That(Q("*.txt").Matches("a.txt"), Is.True);
        Assert.That(Q("a?c").Matches("abc"), Is.True);
        Assert.That(Q("txt").Matches("a.txt"), Is.True);
        Assert.That(Q("size").Matches("my size.txt"), Is.True);
        Assert.That(Q("dm").Matches("dm.txt"), Is.True);
        Assert.That(Q("ratio:3").Matches("ratio:3"), Is.True);
        Assert.That(Q("").RegexExhausted, Is.False);
    }

    private sealed class Utf8Entry(string name, string path, long size = 0, long modifiedTicks = 0) : IEntryText
    {
        private readonly byte[] _name = Encoding.UTF8.GetBytes(name);
        private readonly byte[] _path = Encoding.UTF8.GetBytes(path);
        public bool IsFolder => false;
        public long Size => size;
        public long ModifiedTicks => modifiedTicks;
        public ReadOnlySpan<byte> Name => _name;
        public ReadOnlySpan<byte> Path => _path;
    }
}

using System;
using cdeAppCore.Formatting;
using cdeAppCore.Validation;
using cdeLib.Entities;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class SearchFilterValidatorTests
{
    [Test]
    public void Size_FromGreaterThanTo_IsInvalidWithMessage()
    {
        var r = SearchFilterValidator.ValidateSize(true, 100, true, 50);
        Assert.That(r.IsValid, Is.False);
        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Size));
        Assert.That(r.Message, Is.EqualTo(SearchFilterValidator.FromToSizeMessage));
    }

    [Test]
    public void Size_FromNotGreaterThanTo_IsValid()
    {
        Assert.That(SearchFilterValidator.ValidateSize(true, 50, true, 100).IsValid, Is.True);
        Assert.That(SearchFilterValidator.ValidateSize(true, 100, false, 50).IsValid, Is.True);
    }

    [Test]
    public void Date_FromNotBeforeTo_IsInvalid()
    {
        var r = SearchFilterValidator.ValidateDate(true, new DateTime(2020, 2, 1), true, new DateTime(2020, 1, 1));
        Assert.That(r.IsValid, Is.False);
        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Date));
    }

    [Test]
    public void Hour_FromNotBeforeTo_IsInvalid()
    {
        var r = SearchFilterValidator.ValidateHour(true, TimeSpan.FromHours(10), true, TimeSpan.FromHours(9));
        Assert.That(r.IsValid, Is.False);
        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Hour));
    }

    [Test]
    public void Regex_InvalidPattern_IsInvalidWithMessage()
    {
        var r = SearchFilterValidator.ValidateRegex(true, "(unterminated");
        Assert.That(r.IsValid, Is.False);
        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Regex));
        Assert.That(r.Message, Is.Not.Empty);
    }

    [Test]
    public void Regex_NotInRegexMode_IsValidEvenForBadPattern()
    {
        Assert.That(SearchFilterValidator.ValidateRegex(false, "(unterminated").IsValid, Is.True);
    }

    [Test]
    public void Validate_ReturnsRegexFailureBeforeSizeFailure()
    {
        var r = SearchFilterValidator.Validate(
            regexMode: true, pattern: "(bad",
            fromSizeEnabled: true, fromSize: 100, toSizeEnabled: true, toSize: 50,
            fromDateEnabled: false, fromDate: default, toDateEnabled: false, toDate: default,
            fromHourEnabled: false, fromHour: default, toHourEnabled: false, toHour: default);

        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Regex));
    }
}

[TestFixture]
public class EntryFormatterTests
{
    [Test]
    public void ToHRString_Zero_IsZero()
    {
        Assert.That(0L.ToHRString(), Is.EqualTo("0"));
    }

    [Test]
    public void FormatDirectorySizeCell_File_IsRawSize()
    {
        var file = new DirEntry(false) { Path = "a.txt", Size = 50 };
        Assert.That(EntryFormatter.FormatDirectorySizeCell(file), Is.EqualTo("50"));
    }

    [Test]
    public void FormatDirectorySizeCell_Directory_IsAnnotated()
    {
        var dir = new DirEntry(true) { Path = "dir", Size = 0 };
        Assert.That(EntryFormatter.FormatDirectorySizeCell(dir), Is.EqualTo("0 <Dir>"));
    }

    [Test]
    public void FormatDate_MatchesStringFormat_AndCaches()
    {
        var formatter = new EntryFormatter("{0:yyyy/MM/dd}");
        var date = new DateTime(2011, 12, 2);
        var expected = string.Format("{0:yyyy/MM/dd}", date);

        Assert.That(formatter.FormatDate(date), Is.EqualTo(expected));
        // Second call (cached) returns the same value.
        Assert.That(formatter.FormatDate(date), Is.EqualTo(expected));
    }
}

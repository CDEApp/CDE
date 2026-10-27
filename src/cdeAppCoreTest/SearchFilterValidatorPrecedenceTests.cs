using System;
using cdeAppCore.Validation;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class SearchFilterValidatorPrecedenceTests
{
    private static readonly DateTime Early = new(2020, 1, 1);
    private static readonly DateTime Late = new(2021, 1, 1);

    private static SearchValidationResult Validate(
        bool sizeBad = false, bool dateBad = false, bool hourBad = false)
        => SearchFilterValidator.Validate(
            regexMode: false, pattern: "",
            fromSizeEnabled: true, fromSize: sizeBad ? 10 : 1, toSizeEnabled: true, toSize: sizeBad ? 1 : 10,
            fromDateEnabled: true, fromDate: dateBad ? Late : Early, toDateEnabled: true, toDate: dateBad ? Early : Late,
            fromHourEnabled: true, fromHour: TimeSpan.FromHours(hourBad ? 5 : 1),
            toHourEnabled: true, toHour: TimeSpan.FromHours(hourBad ? 1 : 5));

    [Test]
    public void Date_FromBeforeTo_IsValid()
    {
        Assert.That(SearchFilterValidator.ValidateDate(true, Early, true, Late).IsValid, Is.True);
        Assert.That(SearchFilterValidator.ValidateDate(false, Late, true, Early).IsValid, Is.True);
    }

    [Test]
    public void Hour_FromBeforeTo_IsValid()
    {
        Assert.That(SearchFilterValidator.ValidateHour(true, TimeSpan.FromHours(1), true, TimeSpan.FromHours(2)).IsValid, Is.True);
        Assert.That(SearchFilterValidator.ValidateHour(true, TimeSpan.FromHours(3), false, TimeSpan.FromHours(2)).IsValid, Is.True);
    }

    [Test]
    public void Validate_AllConsistent_IsValid()
    {
        Assert.That(Validate().IsValid, Is.True);
    }

    [Test]
    public void Validate_ReportsSizeFirst()
    {
        var r = Validate(sizeBad: true, dateBad: true, hourBad: true);
        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Size));
    }

    [Test]
    public void Validate_ReportsDateBeforeHour()
    {
        var r = Validate(dateBad: true, hourBad: true);
        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Date));
    }

    [Test]
    public void Validate_ReportsHourLast()
    {
        var r = Validate(hourBad: true);
        Assert.That(r.IsValid, Is.False);
        Assert.That(r.Field, Is.EqualTo(SearchFilterField.Hour));
    }
}

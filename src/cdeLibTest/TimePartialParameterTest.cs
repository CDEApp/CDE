using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest;

[TestFixture]
public class TimePartialParameterTest
{
    [Test]
    public void Hour_Parameter()
    {
        const string args = "12";
        var d = new TimePartialParameter(args);
        Assert.That(d.Hour, Is.EqualTo(12));
        Assert.That(d.Minute, Is.EqualTo(0));
        Assert.That(d.Second, Is.EqualTo(0));
    }

    [Test]
    public void Too_Large_Hour_Parameter()
    {
        const string args = "24";

        var ex = Assert.Throws<ArgumentException>(() => _ = (new TimePartialParameter(args)).Hour);
        Assert.That(ex.Message, Is.EqualTo("Require valid Integer 1-23 for Hour <HH> as part of format '<HH>:<MM>:<SS>'"));
    }

    [Test]
    public void Bad_Hour_Parameter()
    {
        var args = "a";
        var value = 0;

        var ex = Assert.Throws<ArgumentException>(() => value = (new TimePartialParameter(args)).Hour);
        Assert.That(ex.Message, Is.EqualTo("Require valid Integer 1-23 for Hour <HH> as part of format '<HH>:<MM>:<SS>'"));
    }

    [Test]
    public void Hour_With_Minute_Parameter()
    {
        var args = "3:34";
        var d = new TimePartialParameter(args);
        Assert.That(d.Hour, Is.EqualTo(3));
        Assert.That(d.Minute, Is.EqualTo(34));
        Assert.That(d.Second, Is.EqualTo(0));
    }

    [Test]
    public void Hour_With_Bad_Minute_Parameter()
    {
        var args = "3:34s";
        var value = 0;
        var ex = Assert.Throws<ArgumentException>(() => value = (new TimePartialParameter(args)).Hour);
        Assert.That(ex.Message, Is.EqualTo("Require valid integer 1-59 or for Minute <MM> as part of format '<HH>:<MM>:<SS>'"));
    }

    [Test]
    public void Hour_With_Too_Large_Minute_Parameter()
    {
        var args = "3:60";
        var value = 0;
        var ex = Assert.Throws<ArgumentException>(() => value = (new TimePartialParameter(args)).Hour);
        Assert.That(ex.Message, Is.EqualTo("Require valid integer 1-59 or for Minute <MM> as part of format '<HH>:<MM>:<SS>'"));
    }

    [Test]
    public void Hour_With_Minute_With_Second_Parameter()
    {
        var args = "3:34:10";
        var d = new TimePartialParameter(args);
        Assert.That(d.Hour, Is.EqualTo(3));
        Assert.That(d.Minute, Is.EqualTo(34));
        Assert.That(d.Second, Is.EqualTo(10));
    }

    [Test]
    public void Hour_With_Minute_With_Bad_Second_Parameter()
    {
        var args = "3:34:10s";
        var value = 0;
        var ex = Assert.Throws<ArgumentException>(() => value = (new TimePartialParameter(args)).Hour);
        Assert.That(ex.Message, Is.EqualTo("Require valid integer 1-59 or for Second <SS> as part of format '<HH>:<MM>:<SS>'"));
    }

    [Test]
    public void Hour_With_Minute_With_To_Large_Second_Parameter()
    {
        var args = "3:34:60";
        var value = 0;
        var ex = Assert.Throws<ArgumentException>(() => value = (new TimePartialParameter(args)).Hour);
        Assert.That(ex.Message, Is.EqualTo("Require valid integer 1-59 or for Second <SS> as part of format '<HH>:<MM>:<SS>'"));
    }
}
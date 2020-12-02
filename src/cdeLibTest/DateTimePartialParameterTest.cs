using System;
using cdeLib;
using NUnit.Framework;

namespace cdeLibTest
{
    [TestFixture]
    public class DateTimePartialParameterTest
    {
        [Test]
        public void Require_4Digit_Year()
        {
            var args = "12";
            var ex = Assert.Throws<ArgumentException>(() => new DateTimePartialParameter(args).GetDate());
            Assert.That(ex.Message, Is.EqualTo("Require Year parameter be a 4 Digit Year <YYYY> as part of format '<YYYY>-<Month>-<DD>T<HH>:<MM>:<SS>'"));
        }

        [Test]
        public void Year_Only()
        {
            var args = "2014";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 01, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Trailing_Dash()
        {
            var args = "2014-";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 01, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_2Digit_Month()
        {
            var args = "2014-12";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 12, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_1Digit_Month()
        {
            var args = "2014-6";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 06, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Named_Month()
        {
            var args = "2014-May";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 05, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Long_Named_Month()
        {
            var args = "2014-December";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 12, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_BadNamed_Month()
        {
            var args = "2014-Ma";
            Assert.Throws<ArgumentException>(() => new DateTimePartialParameter(args).GetDate());
        }

        [Test]
        public void Year_With_Long_Named_Month_And_Trailing_Dash()
        {
            var args = "2014-December-";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 12, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Month_With_1Digit_Day()
        {
            var args = "2014-4-5";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 04, 05, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Month_With_Bad_Day()
        {
            var args = "2014-2-29";
            var ex = Assert.Throws<ArgumentException>(() => new DateTimePartialParameter(args).GetDate());
            Assert.That(ex.Message, Is.EqualTo("Require valid Day of Month integer range 1-31 for Day <DD> as part of format '<YYYY>-<Month>-<DD>T<HH>:<MM>:<SS>'"));
        }

        [Test]
        public void Year_With_Month_Day_And_T()
        {
            var args = "2014-4-5T";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 04, 05, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Month_Day_And_Bad_Z()
        {
            var args = "2014-4-5Z"; // todo identify Z as problem not day integer maybe ?
            var ex = Assert.Throws<ArgumentException>(() => new DateTimePartialParameter(args).GetDate());
            Assert.That(ex.Message, Is.EqualTo("The separator between Date and Time must be 'T' as part of format '<YYYY>-<Month>-<DD>T<HH>:<MM>:<SS>'"));
        }

        [Test]
        public void YMD_With_Hour()
        {
            var args = "2014-4-5T12";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 04, 05, 12, 00, 00)));
        }

        [Test]
        public void YMD_With_Hour_Minute()
        {
            var args = "2014-4-5T12:45";
            var d = new DateTimePartialParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 04, 05, 12, 45, 00)));
        }
    }
}
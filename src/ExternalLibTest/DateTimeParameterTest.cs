using System;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;

namespace ExternalLibTest
{
    [TestFixture]
    [SuppressMessage("ReSharper", "ConvertToConstant.Local")]
    public class DateTimeParameterTest
    {
        [Test]
        public void Require_4Digit_Year()
        {
            var args = "12";
            Assert.Throws<ArgumentException>(() => new DateTimeParameter(args).GetDate());
        }

        [Test]
        public void Year_Only()
        {
            var args = "2014";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 01, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Trailing_Dash()
        {
            var args = "2014-";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 01, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_2Digit_Month()
        {
            var args = "2014-12";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 12, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_1Digit_Month()
        {
            var args = "2014-6";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 06, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Named_Month()
        {
            var args = "2014-May";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 05, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Long_Named_Month()
        {
            var args = "2014-December";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 12, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_BadNamed_Month()
        {
            var args = "2014-Ma";
            Assert.Throws<ArgumentException>(() => new DateTimeParameter(args).GetDate());
        }

        [Test]
        public void Year_With_Long_Named_Month_And_Trailing_Dash()
        {
            var args = "2014-December-";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 12, 01, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Month_With_1Digit_Day()
        {
            var args = "2014-4-5";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 04, 05, 00, 00, 00)));
        }

        [Test]
        public void Year_With_Month_With_Bad_Day()
        {
            var args = "2014-2-29";
            Assert.Throws<ArgumentException>(() => new DateTimeParameter(args).GetDate());
        }

        [Test]
        public void Year_With_Month_Day_And_T()
        {
            var args = "2014-4-5T";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 04, 05, 00, 00, 00)));
        }

        [Test]
        public void YMD_With_Hours()
        {
            var args = "2014-4-5T12";
            var d = new DateTimeParameter(args).GetDate();
            Assert.That(d, Is.EqualTo(new DateTime(2014, 04, 05, 00, 00, 00)));
        }
    }
}
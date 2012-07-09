using System.Collections.Generic;
using NUnit.Framework;
using Rhino.Mocks;
using Util;
using cdeLib;
using cdeWin;

namespace cdeWinTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class TestCDEWinPresenter_OptimiseRegexPattern_NotRegex : TestCDEWinPresenterBase
    {
        protected TestOptimise _presenter;

        [SetUp]
        public override void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();
            _mockForm.Stub(x => x.RegexMode).Return(false);
            _presenter = new TestOptimise(_mockForm, new List<RootEntry>(), _stubConfig, null);
        }

        [Test]
        public void OptimiseRegexPattern_NullInput_ReturnsNull_WhenNotRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(null);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void OptimiseRegexPattern_EmptyInput_ReturnsEmpty_WhenNotRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(string.Empty);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void OptimiseRegexPattern_LeadingWild_ReturnsUnchanged_WhenNotRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(".*moo");

            Assert.That(result, Is.EqualTo(".*moo"));
        }

        [Test]
        public void OptimiseRegexPattern_TrailingWild_ReturnsUnchanged_WhenNotRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern("moo.*");

            Assert.That(result, Is.EqualTo("moo.*"));
        }
    }


    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class TestCDEWinPresenter_OptimiseRegexPattern_Regex : TestCDEWinPresenterBase
    {
        protected TestOptimise _presenter;

        [SetUp]
        override public void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();
            _mockForm.Stub(x => x.RegexMode).Return(true);
            _presenter = new TestOptimise(_mockForm, new List<RootEntry>(), _stubConfig, null);
        }
        
        [Test]
        public void OptimiseRegexPattern_NullInput_ReturnsNull_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(null);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void OptimiseRegexPattern_EmptyInput_ReturnsEmpty_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(string.Empty);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void OptimiseRegexPattern_LeadingWild_ReturnsWithout_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(".*moo");

            Assert.That(result, Is.EqualTo("moo"));
        }


        [Test]
        public void OptimiseRegexPattern_TrailingWild_ReturnsWithout_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern("moo.*");

            Assert.That(result, Is.EqualTo("moo"));
        }

        [Test]
        public void OptimiseRegexPattern_MultipleWildInMiddle_ReturnsWithout_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(".*m.*oo.*");

            Assert.That(result, Is.EqualTo("m.*oo"));
        }
    }

    public class TestOptimise : CDEWinFormPresenter
    {
        public TestOptimise(ICDEWinForm form, List<RootEntry> rootEntries, IConfig config, TimeIt timeIt) : base(form, rootEntries, config, timeIt)
        {
        }

        public string TestOptimiseRegexPattern(string pattern)
        {
            return OptimiseRegexPattern(pattern);
        }
    }

    // ReSharper restore InconsistentNaming
}


using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rhino.Mocks;
using cdeLib;
using cdeWin;

namespace cdeWinTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class TestCDEWinPresenter_OptimiseRegexPattern
    {
        private TestOptimise _presenter;
        private ICDEWinForm _stubForm;
        private IConfig _stubConfig;

        [SetUp]
        public void RunBeforeEveryTest()
        {
            _stubForm = MockRepository.GenerateStub<ICDEWinForm>();
            _stubConfig = MockRepository.GenerateStub<IConfig>();
            _stubForm.RegexMode = true;
            _presenter = new TestOptimise(_stubForm, new List<RootEntry>(), _stubConfig);  
        }

        [Test]
        public void OptimiseRegexPattern_NullInput_ReturnsNull_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(null);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void OptimiseRegexPattern_NullInput_ReturnsNull_WhenNotRegexMode()
        {
            _stubForm.RegexMode = false;

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
        public void OptimiseRegexPattern_EmptyInput_ReturnsEmpty_WhenNotRegexMode()
        {
            _stubForm.RegexMode = false;

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
        public void OptimiseRegexPattern_LeadingWild_ReturnsUnchanged_WhenNotRegexMode()
        {
            _stubForm.RegexMode = false;

            var result = _presenter.TestOptimiseRegexPattern(".*moo");

            Assert.That(result, Is.EqualTo(".*moo"));
        }

        [Test]
        public void OptimiseRegexPattern_TrailingWild_ReturnsWithout_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern("moo.*");

            Assert.That(result, Is.EqualTo("moo"));
        }

        [Test]
        public void OptimiseRegexPattern_TrailingWild_ReturnsUnchanged_WhenNotRegexMode()
        {
            _stubForm.RegexMode = false;

            var result = _presenter.TestOptimiseRegexPattern("moo.*");

            Assert.That(result, Is.EqualTo("moo.*"));
        }

        [Test]
        public void OptimiseRegexPattern_MultipleWildInMiddle_ReturnsWithout_WhenRegexMode()
        {
            var result = _presenter.TestOptimiseRegexPattern(".*m.*oo.*");

            Assert.That(result, Is.EqualTo("m.*oo"));
        }

        public class TestOptimise : CDEWinFormPresenter
        {
            public TestOptimise(ICDEWinForm form, List<RootEntry> rootEntries, IConfig config) : base(form, rootEntries, config)
            {
            }

            public string TestOptimiseRegexPattern(string pattern)
            {
                return OptimiseRegexPattern(pattern);
            }
        }
    }
    // ReSharper restore InconsistentNaming
}


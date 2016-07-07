using System;
using cdeLib;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using TestStack.BDDfy;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace cdeLibSpec2
{
    [TestFixture]
    [Story(
        AsA = "As a process I wish to find Directory Entries",
        IWant = "I want FindOptions in a specific default state",
        SoThat = "I can just set the options I need to change")]
    public class FindBehaviour
    {
        [Test]
        public void CheckDefaultFindOptionValues()
        {
            new Check_default_FindOptions_values().BDDfy();
        }

        [Test]
        public void CheckFindsFoundBeingCalledImmediatelyBeforeLimit()
        {
            new CheckFoundCalled().BDDfy();
        }

        [Test]
        public void CheckFindsReturnImmediatelyBeforeLimit()
        {
            new CheckFindReturn().BDDfy();
        }
    }

    public class Setup_Basic_Find
    {
        protected FindOptions _findOptions;
        protected TraverseFunc _visitorFunc;
        protected TraverseFunc _findFunc;
        protected RootEntry _rootEntry;
        protected DirEntry _testFile;
        protected DirEntry _testDir;

        public void Given_FindOptions_config_match_all()
        {
            _findOptions = new FindOptions();
            _visitorFunc = Substitute.For<TraverseFunc>();
            var matcherAll = Substitute.For<Func<CommonEntry, DirEntry, bool>>();
            matcherAll(null, null).ReturnsForAnyArgs(true);

            _findOptions.VisitorFunc = _visitorFunc;
            _findOptions.PatternMatcher = matcherAll;

            _rootEntry = new RootEntry {FullPath = @"C:\"};
            _testFile = new DirEntry(false) {Path = @"TestFile1"};
            _testDir = new DirEntry(true) {Path = @"TestDir"};
        }
    }

    public class JustBeforeLimit : Setup_Basic_Find
    {
        public void And_given_Find_immediately_before_limit()
        {
            _findFunc = _findOptions.GetFindFunc(new[] {0}, new[] {1});
        }
    }

    public class CheckFoundCalled : JustBeforeLimit
    {
        void When_findFunc_called_on_file()
        {
            _findFunc(_rootEntry, _testFile);
        }

        void Then_Found_visitor_gets_that_found_file()
        {
            _findOptions.VisitorFunc.Received()(_rootEntry, _testFile);
        }
    }

    public class CheckFindReturn : JustBeforeLimit
    {
        void When_findFunc_called_on_file()
        {
            _visitorFunc(null, null).ReturnsForAnyArgs(true);
        }

        void Then_Found_visitor_returns_false_to_indicate_limit_reached()
        {
            _findFunc(_rootEntry, _testFile).ShouldBeFalse();
        }
    }

    // Try out Fluent, maybe a bit easier to reuse.
    // but just as many pieces of code.
}
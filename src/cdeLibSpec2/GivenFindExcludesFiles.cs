using System;
using cdeLib;
using NSubstitute;
using NUnit.Framework;
using TestStack.BDDfy;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace cdeLibSpec2
{
    public class GivenFindExcludesFiles 
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

            _rootEntry = new RootEntry { FullPath = @"C:\" };
            _testFile = new DirEntry(false) { Path = @"TestFile1" };
            _testDir = new DirEntry(true) { Path = @"TestDir" };
        }

        void And_given_Find_excludes_files()
        {
            _findOptions.IncludeFiles = false;
            _findFunc = _findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
        }

        void When_findFunc_called_on_folder()
        {
            _findFunc(_rootEntry, _testDir);
        }

        void Then_Find_on_folder_calls_found()
        {
            _findOptions.VisitorFunc.ReceivedWithAnyArgs().Invoke(null, null);
        }


        void And_given_Find_excludes_folders()
        {
            _findOptions.IncludeFolders = false;
            _findFunc = _findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
        }

        void Then_Find_of_dir_does_not_call_found()
        {
            _findOptions.VisitorFunc.DidNotReceiveWithAnyArgs().Invoke(null, null);
        }

        [Test]
        public void FindOfFolderFoundWithFilesExcluded()
        {
            this.Given(s => s.Given_FindOptions_config_match_all())
                .And(s => s.And_given_Find_excludes_files())
                .When(s => s.When_findFunc_called_on_folder())
                .Then(s => s.Then_Find_on_folder_calls_found())
                .BDDfy<FindBehaviour>();
        }

        [Test]
        public void FindOfFolderNotFoundWithFoldersExcluded()
        {
            this.Given(s => s.Given_FindOptions_config_match_all())
                .And(s => s.And_given_Find_excludes_folders())
                .When(s => s.When_findFunc_called_on_folder())
                .Then(s => s.Then_Find_of_dir_does_not_call_found())
                .BDDfy<FindBehaviour>();
        }
    }
}
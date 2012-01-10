using System;
using NUnit.Framework;
using Rhino.Mocks;
using cdeLib;
using cdeWin;

namespace cdeWinTest
{
    // ReSharper disable InconsistentNaming
    public class TestCDEWinPresenterBase
    {
        protected ICDEWinForm _mockForm;
        protected IConfig _stubConfig;
        protected IListViewHelper<PairDirEntry> _mockSearchResultListViewHelper;
        protected IListViewHelper<DirEntry> _mockDirectoryListViewHelper;
        protected IListViewHelper<RootEntry> _mockCatalogListViewHelper;

        protected RootEntry _rootEntry;
        protected DirEntry _dirEntry;
        protected PairDirEntry _pairDirEntry;

        [SetUp]
        public virtual void RunBeforeEveryTest()
        {
            _mockForm = MockRepository.GenerateMock<ICDEWinForm>();
            _stubConfig = MockRepository.GenerateStub<IConfig>();

            _mockSearchResultListViewHelper = MockRepository.GenerateMock<IListViewHelper<PairDirEntry>>();
            _mockForm.Stub(x => x.SearchResultListViewHelper)
                .Return(_mockSearchResultListViewHelper);

            _mockDirectoryListViewHelper = MockRepository.GenerateMock<IListViewHelper<DirEntry>>();
            _mockForm.Stub(x => x.DirectoryListViewHelper)
                .Return(_mockDirectoryListViewHelper);

            _mockCatalogListViewHelper = MockRepository.GenerateMock<IListViewHelper<RootEntry>>();
            _mockForm.Stub(x => x.CatalogListViewHelper)
                .Return(_mockCatalogListViewHelper);
        }

        protected void InitRootWithFile()
        {
            _rootEntry = new RootEntry {
                Path = @"T:\",
                VolumeName = "TestVolume",
                DirCount = 1,
                FileCount = 0,
                DriveLetterHint = @"Z",
                AvailSpace = 754321,
                UsedSpace = 654321,
                ScanStartUTC = new DateTime(2011, 12, 01, 17, 15, 13, DateTimeKind.Utc),
                DefaultFileName = "TestRootEntry.cde",
                SourcePath= @"C:\Users\testuser\AppData\Local\cde",
                Description = "Test Root Entry Description",
            };

            _dirEntry = new DirEntry
            {
                Path = @"Test",
                Size = 531,
                Modified = new DateTime(2010, 11, 02, 18, 16, 12, DateTimeKind.Unspecified),
            };
            _rootEntry.Children.Add(_dirEntry);
            _rootEntry.SetInMemoryFields();
            _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);
        }

        protected void InitRootWithDir()
        {
            _rootEntry = new RootEntry { Path = @"T:\" };
            _dirEntry = new DirEntry(true) { Path = @"Test1" };
            _rootEntry.Children.Add(_dirEntry);
            _rootEntry.SetInMemoryFields();
            _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);
        }

        protected void InitRootWithDirDirFileWithDir()
        {
            InitRootWithDir();
            var dirEntry2 = new DirEntry(true) { Path = @"Test2" };
            var dirEntry3 = new DirEntry(true) { Path = @"Test3" };
            var dirEntry4 = new DirEntry { Path = @"Test4" };
            _dirEntry.Children.Add(dirEntry2);
            _rootEntry.Children.Add(dirEntry3);
            _rootEntry.Children.Add(dirEntry4);
            _rootEntry.SetInMemoryFields();
        }

        protected Action<T> GetPresenterAction<T>(IListViewHelper<T> lvh) where T : class
        {
            var args = lvh.GetArgumentsForCallsMadeOn(
                x => x.ActionOnActivateItem(Arg<Action<T>>.Is.Anything));
            Assert.That(args.Count, Is.EqualTo(1));
            Assert.That(args[0].Length, Is.EqualTo(1));
            return (Action<T>)(args[0][0]); // extract the ActivateOnItem action 
        }

        // ReSharper disable LocalizableElement
        protected void TracePresenterAction<T>(IListViewHelper<T> lvh) where T : class
        {
            lvh.Stub(x => x.ActionOnActivateItem(Arg<Action<T>>.Is.Anything))
                .WhenCalled(a => Console.WriteLine("called ActionOnActivateItem()."));
        }
        // ReSharper restore LocalizableElement
    }
    // ReSharper restore InconsistentNaming
}
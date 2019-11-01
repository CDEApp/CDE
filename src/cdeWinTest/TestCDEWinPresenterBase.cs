using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NUnit.Framework;
using cdeLib;
using cdeWin;
using NSubstitute;

namespace cdeWinTest
{
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

        protected List<RootEntry> _emptyRootList = new List<RootEntry>();
        protected List<RootEntry> _rootList = new List<RootEntry>();
        protected TreeNode _treeViewAfterSelectNode;

        [SetUp]
        public virtual void RunBeforeEveryTest()
        {
             _emptyRootList = new List<RootEntry>();
             _rootList = new List<RootEntry>();

            _mockForm = Substitute.For<ICDEWinForm>();
            _stubConfig = Substitute.For<IConfig>();
            _mockSearchResultListViewHelper = Substitute.For<IListViewHelper<PairDirEntry>>();
            _mockForm.SearchResultListViewHelper.Returns(_mockSearchResultListViewHelper);
            _mockDirectoryListViewHelper = Substitute.For<IListViewHelper<DirEntry>>();
            _mockForm.DirectoryListViewHelper.Returns(_mockDirectoryListViewHelper);
            _mockCatalogListViewHelper = Substitute.For<IListViewHelper<RootEntry>>();
            _mockForm.CatalogListViewHelper.Returns(_mockCatalogListViewHelper);
        }

        protected void InitRootWithFile()
        {
            var nowUtc = new DateTime(2011, 12, 01, 17, 15, 13, DateTimeKind.Utc);
            _rootEntry = new RootEntry {
                Path = @"T:\",
                VolumeName = "TestVolume",
                DirEntryCount = 1,
                FileEntryCount = 0,
                DriveLetterHint = @"Z",
                AvailSpace = 754321,
                TotalSpace = 654321,
                ScanStartUTC = nowUtc,
                ScanEndUTC = nowUtc.AddMilliseconds(34),
                DefaultFileName = "TestRootEntry.cde",
                ActualFileName = @".\TestRootEntry.cde",
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

            _rootList.Add(_rootEntry);
        }

        protected void InitRootWithDir()
        {
            _rootEntry = new RootEntry { Path = @"T:\" };
            _dirEntry = new DirEntry(true) { Path = @"Test1" };
            _rootEntry.Children.Add(_dirEntry);
            _rootEntry.SetInMemoryFields();
            _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);

            _rootList.Add(_rootEntry);
        }

        protected void InitRootWithDirDirFileWithDir()
        {
            InitRootWithDir();
            var dirEntry2 = new DirEntry(true) { Path = @"Test2" };
            var dirEntry3 = new DirEntry(true) 
            {
                Path = @"Test3",
                Size = 12312,
                Modified = new DateTime(2009, 10, 03, 19, 17, 13, DateTimeKind.Unspecified),
            };
            var dirEntry4 = new DirEntry
            {
                Path = @"Test4",
                Size = 32312,
                Modified = new DateTime(2008, 10, 04, 20, 18, 14, DateTimeKind.Unspecified),
            };
            _dirEntry.Children.Add(dirEntry2);
            _rootEntry.Children.Add(dirEntry3);
            _rootEntry.Children.Add(dirEntry4);
            _rootEntry.SetInMemoryFields();
            _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);

            _rootList.Add(_rootEntry);
        }

        // protected Action<T> GetPresenterAction<T>(IListViewHelper<T> lvh) where T : class
        // {
        //     var args = lvh.GetArgumentsForCallsMadeOn(
        //         x => x.ActionOnActivateItem(Arg<Action<T>>.Is.Anything));
        //     Assert.That(args.Count, Is.EqualTo(1));
        //     Assert.That(args[0].Length, Is.EqualTo(1));
        //     return (Action<T>)(args[0][0]); // extract the ActivateOnItem action 
        // }

        // // ReSharper disable LocalizableElement
        // protected void TracePresenterAction<T>(IListViewHelper<T> lvh) where T : class
        // {
        //     lvh.Stub(x => x.ActionOnActivateItem(Arg<Action<T>>.Is.Anything))
        //         .WhenCalled(a => Console.WriteLine("called ActionOnActivateItem()."));
        // }
        // // ReSharper restore LocalizableElement

        // protected void MockTreeViewAfterSelect(CDEWinFormPresenter presenter)
        // {
        //     _mockForm.Stub(x => x.DirectoryTreeViewSelectedNode = Arg<TreeNode>.Is.Anything)
        //         .WhenCalled(a =>
        //                         {
        //                             _treeViewAfterSelectNode = (TreeNode)a.Arguments[0];
        //                             _mockForm.Stub(x => x.DirectoryTreeViewActiveAfterSelectNode)
        //                                 .Repeat.Times(1)
        //                                 .Return(_treeViewAfterSelectNode);
        //                             presenter.DirectoryTreeViewAfterSelect();
        //                         });
        // }
    }

    // ReSharper restore InconsistentNaming
}
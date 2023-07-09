using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NUnit.Framework;
using cdeLib;
using cdeLib.Entities;
using cdeLib.Infrastructure.Config;
using cdeWin;
using NSubstitute;

namespace cdeWinTest;

public class TestCDEWinPresenterBase
{
    // ReSharper disable InconsistentNaming
    protected ICDEWinForm _mockForm;
    protected IConfig _stubConfig;
    protected IListViewHelper<PairDirEntry> _mockSearchResultListViewHelper;
    protected IListViewHelper<ICommonEntry> _mockDirectoryListViewHelper;
    protected IListViewHelper<RootEntry> _mockCatalogListViewHelper;

    protected RootEntry _rootEntry;
    protected DirEntry _dirEntry;
    protected PairDirEntry _pairDirEntry;

    protected List<RootEntry> _emptyRootList = new();
    protected List<RootEntry> _rootList = new();
    // protected TreeNode _treeViewAfterSelectNode;
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    [SetUp]
    public virtual void RunBeforeEveryTest()
    {
        _emptyRootList = new List<RootEntry>();
        _rootList = new List<RootEntry>();
        _config.ProgressUpdateInterval.Returns(5000);

        _mockForm = Substitute.For<ICDEWinForm>();
        _stubConfig = Substitute.For<IConfig>();
        _mockSearchResultListViewHelper = Substitute.For<IListViewHelper<PairDirEntry>>();
        _mockForm.SearchResultListViewHelper.Returns(_mockSearchResultListViewHelper);
        _mockDirectoryListViewHelper = Substitute.For<IListViewHelper<ICommonEntry>>();
        _mockForm.DirectoryListViewHelper.Returns(_mockDirectoryListViewHelper);
        _mockCatalogListViewHelper = Substitute.For<IListViewHelper<RootEntry>>();
        _mockForm.CatalogListViewHelper.Returns(_mockCatalogListViewHelper);
    }

    protected void InitRootWithFile()
    {
        var nowUtc = new DateTime(2011, 12, 01, 17, 15, 13, DateTimeKind.Utc);
        _rootEntry = new RootEntry(_config)
        {
            Path = @"T:\",
            // VolumeName = "TestVolume",
            DirEntryCount = 1,
            FileEntryCount = 0,
            DriveLetterHint = "Z",
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
            Path = "Test",
            Size = 531,
            Modified = new DateTime(2010, 11, 02, 18, 16, 12, DateTimeKind.Unspecified),
        };
        _rootEntry.AddChild(_dirEntry);
        _rootEntry.SetInMemoryFields();
        _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);

        _rootList.Add(_rootEntry);
    }

    protected void InitRootWithDir()
    {
        // massive assumption on path, this T:\ is windows only......
        // is it a valid test on other platforms or behavior on other platforms?
        _rootEntry = new RootEntry(_config) { Path = @"T:\" };
        _dirEntry = new DirEntry(true) { Path = "Test1" };
        _rootEntry.AddChild(_dirEntry);
        _rootEntry.SetInMemoryFields();
        _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);

        _rootList.Add(_rootEntry);
    }

    protected void InitRootWithDirDirFileWithDir()
    {
        InitRootWithDir();
        var dirEntry2 = new DirEntry(true) { Path = "Test2" };
        var dirEntry3 = new DirEntry(true)
        {
            Path = "Test3",
            Size = 12312,
            Modified = new DateTime(2009, 10, 03, 19, 17, 13, DateTimeKind.Unspecified)
        };
        var dirEntry4 = new DirEntry
        {
            Path = "Test4",
            Size = 32312,
            Modified = new DateTime(2008, 10, 04, 20, 18, 14, DateTimeKind.Unspecified)
        };
        _dirEntry.AddChild(dirEntry2);
        _rootEntry.AddChild(dirEntry3);
        _rootEntry.AddChild(dirEntry4);
        _rootEntry.SetInMemoryFields();
        _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);

        _rootList.Add(_rootEntry);
    }

    protected void TracePresenterAction<T>(IListViewHelper<T> lvh) where T : class
    {
        lvh.ActionOnActivateItem(Arg.Do<Action<T>>(_ => Console.Out.WriteLine("called ActionOnActivateItem()")));
    }

    protected void MockDirectoryTreeViewAfterSelect(
        CDEWinFormPresenter presenter,
        Action<TreeNode> captureNode = null)
    {
        _mockForm.DirectoryTreeViewSelectedNode =
            Arg.Do<TreeNode>(node =>
            {
                captureNode?.Invoke(node);
                // these 2 lines simulate the form after select node handler
                _mockForm.DirectoryTreeViewActiveAfterSelectNode = node;
                presenter.DirectoryTreeViewAfterSelect();
            });
    }

    protected void FakeItemActivateWithValue<T>(IListViewHelper<T> view, T value) where T : class
    {
        view.ActionOnActivateItem(Arg.Invoke(value));
    }
}

// ReSharper restore InconsistentNaming
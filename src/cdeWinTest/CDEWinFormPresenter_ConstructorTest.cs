using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NUnit.Framework;
using Rhino.Mocks;
using cdeLib;
using cdeWin;
using Is = NUnit.Framework.Is;

//using Is = NUnit.Framework.Is;

//using Is = NUnit.Framework.Is;

namespace cdeWinTest
{
    public static class RootEntryTestSource
    {
        //
        // holding on to this for now as it was hard to figure out :).....
        //
        //// this was working -- adding a getter to property breaks this 
        //// BROKE cause it was Stub not Mock - odd it breaks only once add getter though.
        //_stubForm.AssertWasCalled(x => x.DirectoryTreeViewNodes
        //    = Arg<TreeNode>.Matches( new PredicateConstraint<TreeNode>(
        //            y => y.Text == testRootPath
        //                 && y.Nodes.Count == 1
        //                 && y.Nodes[0].Text == DummyNodeName )));

        public const string TestRootPath = @"C:\Testing";
        public const string TestDirName = @"TestDir";
        public const string DummyNodeName = @"_dummyNode";

        public static readonly List<RootEntry> RootList = new List<RootEntry> { CreateTestRoot() };
        public static readonly List<RootEntry> RootListEmpty = new List<RootEntry>();

        public static RootEntry CreateTestRoot()
        {
            var rootEntry = new RootEntry { Path = TestRootPath };
            var dirEntry1 = new DirEntry(true)
            {
                Path = TestDirName,
                Modified = new DateTime(2011, 04, 14, 21, 09, 04, DateTimeKind.Local)
            };
            var dirEntry2 = new DirEntry
            {
                Path = "TestFile",
                Size = 3311,
                Modified = new DateTime(2011, 03, 13, 22, 10, 5, DateTimeKind.Local)
            };
            rootEntry.Children.Add(dirEntry1);
            rootEntry.Children.Add(dirEntry2);
            rootEntry.SetInMemoryFields();
            return rootEntry;
        }
    }

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CDEWinFormPresenter_ConstructorTest : TestCDEWinPresenterBase
    {
        [SetUp]
        override public void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();
        }

        [Test]
        public void Constructor_WithNullRootEntriesList_OK()
        {
            _mockForm.Stub(x => x.SetList(Arg<ListViewHelper<RootEntry>>.Is.Anything, Arg<List<RootEntry>>.Is.Same(null)))
                .Repeat.Times(1)
                .Return(1);
            _mockForm.Stub(x => x.SortList(Arg<ListViewHelper<RootEntry>>.Is.Anything))
                .Repeat.Times(1);
            _mockForm.Stub(x => x.SetCatalogsLoadedStatus(Arg<int>.Is.Anything))
                .Repeat.Times(1);

            new CDEWinFormPresenter(_mockForm, null, _stubConfig);

            _mockForm.VerifyAllExpectations();
        }

        [Test]
        public void Constructor_WithEmptyRootEntriesList_OK()
        {
            _mockForm.Stub(x => x.SetList(Arg<ListViewHelper<RootEntry>>.Is.Anything, Arg<List<RootEntry>>.Is.Same(RootEntryTestSource.RootListEmpty)))
                .Repeat.Times(1)
                .Return(1);
            _mockForm.Stub(x => x.SortList(Arg<ListViewHelper<RootEntry>>.Is.Anything))
                .Repeat.Times(1);
            _mockForm.Stub(x => x.SetCatalogsLoadedStatus(Arg<int>.Is.Anything))
                .Repeat.Times(1);

            new CDEWinFormPresenter(_mockForm, RootEntryTestSource.RootListEmpty, _stubConfig);

            _mockForm.VerifyAllExpectations();
        }

        [Test]
        public void Constructor_WithValidRootEntriesList_OK()
        {
            _mockForm.Stub(x => x.SetList(Arg<ListViewHelper<RootEntry>>.Is.Anything, Arg<List<RootEntry>>.Is.Same(RootEntryTestSource.RootList)))
                .Repeat.Times(1)
                .Return(1);
            _mockForm.Stub(x => x.SortList(Arg<ListViewHelper<RootEntry>>.Is.Anything))
                .Repeat.Times(1);
            _mockForm.Stub(x => x.SetCatalogsLoadedStatus(Arg<int>.Is.Anything))
                .Repeat.Times(1);

            new CDEWinFormPresenter(_mockForm, RootEntryTestSource.RootList, _stubConfig);

            _mockForm.VerifyAllExpectations();
        }

        //[Test]
        //public void ExpandRootNode_PoplulatesFirstLevelChildren()
        //{
        //    //_testPresenter.LoadData();
        //    Assert.Fail("LoadData() no longer avail it was a hack");

        //    var rootNode = _mockForm.DirectoryTreeViewNodes;
        //    _mockForm.DirectoryTreeViewActiveBeforeExpandNode = rootNode;

        //    _testPresenter.DirectoryTreeViewBeforeExpandNode();

        //    //   Assert.That(rootNode.Nodes.Count, Is.EqualTo(1));
        //    //   Assert.That(rootNode.Nodes[0].Text, Is.EqualTo(testDirName));
        //    _mockForm.AssertWasCalled(x => x.DirectoryTreeViewNodes
        //        = Arg<TreeNode>.Matches(new PredicateConstraint<TreeNode>(
        //                    y => y.Text == testRootPath
        //                     && y.Nodes.Count == 1
        //                     && y.Nodes[0].Text == testDirName)));

        //}
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CDEWinFormPresenter_DirectoryTreeViewBeforeExpandNode : TestCDEWinPresenterBase
    {
        private CDEWinFormPresenter _sutPresenter;

        [SetUp]
        override public void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();
            _sutPresenter = new CDEWinFormPresenter(_mockForm, RootEntryTestSource.RootList, _stubConfig);
        }

        // this shouldnt happen.
        [Test]
        public void DirectoryTreeViewBeforeExpandNode_WithNoTreeNodeSet_DoesNothing_DoesNotFail()
        {
            _mockForm.Stub(x => x.DirectoryTreeViewActiveBeforeExpandNode)
                .Repeat.Times(1)
                .Return(new TreeNode("testTreeNode"));

            _sutPresenter.DirectoryTreeViewBeforeExpandNode();

            _mockForm.VerifyAllExpectations();
            Assert.Fail("unfinished, thinking of how to test all the private methods hmmm or if i treat as blob ?");
        }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CDEWinFormPresenter_SearchResultListViewItemActivate : TestCDEWinPresenterBase
    {
        private CDEWinFormPresenter _sutPresenter;

        [SetUp]
        override public void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();
            _sutPresenter = new CDEWinFormPresenter(_mockForm, RootEntryTestSource.RootList, _stubConfig);
            InitRoot();
        }

        // TODO another test, but  Root -> DIR -> DIR - to hit antoher part of SetDiretoryWithExpand(). so complex.
        //   this one woudl test CreateNodesPreExpand() 
        //   and Expand() behaviour of node... ? which doesnt matter to listview so maybe irrelevant
        //       but stopping at a tree - without a listview file select is ok. ? 
        //
        // Real values of .Tag setting on TreeNodes etc, as part of presenter.
        // Using real BuildRootNode() as part of presenter.
        //
        //
        // Hmmm this is asserting a lot in one [Test].
        // - break it up into multiple tests 1 assert each ?
        //
        //
        [Test]
        public void SearchResultListViewItemActivate_Callback__ViewFileInDirectoryTab_OK()
        {
            Object treeViewRootNodeArg = null;
            Object listViewListArg = null;

            // Directory TreeView gets a new root node, since there is no previous one loaded.
            _mockForm.Stub(x => x.DirectoryTreeViewNodes = Arg<TreeNode>.Is.Anything)
                .Repeat.Times(1)
                .WhenCalled(a => treeViewRootNodeArg = a.Arguments[0]);

            // When the TreeView node is selected ensure the AfterSelect event occurs as it does in gui.
            MockTreeViewAfterSelect(_sutPresenter);

            // Directory ListView gets children of root
            _mockDirectoryListViewHelper.Stub(x => x.SetList(Arg<List<DirEntry>>.Is.Anything))
                .Repeat.Times(1)
                .Return(1).WhenCalled(a => listViewListArg = a.Arguments[0]);

            // Select the item in list view
            _mockDirectoryListViewHelper.Stub(x => x.SelectItem(Arg<int>.Is.Anything))
                .Repeat.Times(1);

            // Go to Directory pane
            _mockForm.Stub(x => x.SelectDirectoryPane())
                .Repeat.Times(1);

            //TracePresenterAction(_stubSearchResultListViewHelper);

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with SearchResultListView our activated item is a PairDirEntry.
            _sutPresenter.SearchResultListViewItemActivate();
            var action = GetPresenterAction(_stubSearchResultListViewHelper);
            action(_pairDirEntry);

            _mockDirectoryListViewHelper.VerifyAllExpectations();
            _mockForm.VerifyAllExpectations();
            Assert.That(_treeViewAfterSelectNode.Text, Is.EqualTo(@"T:\"), "ListView selected node text value not expected.");

            var treeViewRootNode = (TreeNode) treeViewRootNodeArg;
            Assert.That(treeViewRootNode.Text, Is.EqualTo(@"T:\"), "TreeView root node Text value not expected");

            var list = (List<DirEntry>)listViewListArg;
            Assert.That(list.Count, Is.EqualTo(1), "The list set on ListViewHelper wasnt expected");
            Assert.That(list[0].Path, Is.EqualTo("Test"), "The list set on ListViewHelper wasnt expected");
        }

        [Test]
        public void SearchResultListViewItemActivate_Callback__ViewFileInDirectoryTab_On_Same_RootEntry_Does_Not_Set_Root()
        {
            var testRootTreeNode = new TreeNode("Moo") { Tag = _rootEntry };
            _mockForm.Stub(x => x.DirectoryTreeViewNodes)
                .Repeat.Times(1)    // enforcing repeat makes this a fragile test ?
                .Return(testRootTreeNode);

            // verify does not set DirectoryTreeViewNodes
            _mockForm.Stub(x => x.DirectoryTreeViewNodes = Arg<TreeNode>.Is.Anything)
                .WhenCalled(a => Assert.Fail("GoToDirectoryRoot tried to set DirectoryTreeViewNodes"));

            // When the TreeView node is selected ensure the AfterSelect event occurs as it does in gui.
            MockTreeViewAfterSelect(_sutPresenter);

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with SearchResultListView our activated item is a PairDirEntry.
            _sutPresenter.SearchResultListViewItemActivate();
            var action = GetPresenterAction(_stubSearchResultListViewHelper);
            action(_pairDirEntry);

            _mockForm.VerifyAllExpectations();
        }

        protected void MockTreeViewAfterSelect(CDEWinFormPresenter presenter)
        {
            _mockForm.Stub(x => x.DirectoryTreeViewSelectedNode = Arg<TreeNode>.Is.Anything)
                .WhenCalled(a =>
                {
                    _treeViewAfterSelectNode = (TreeNode)a.Arguments[0];
                    _mockForm.Stub(x => x.DirectoryTreeViewActiveAfterSelectNode)
                        .Repeat.Times(1)
                        .Return(_treeViewAfterSelectNode);
                    presenter.DirectoryTreeViewAfterSelect();
                });
        }
    }
    // ReSharper restore InconsistentNaming

    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CDEWinFormPresenter_CatalogListViewItemActivate : TestCDEWinPresenterBase
    {
        private CDEWinFormPresenter _sutPresenter;

        [SetUp]
        public override void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();
            _sutPresenter = new CDEWinFormPresenter(_mockForm, RootEntryTestSource.RootList, _stubConfig);
            InitRoot();
        }

        [Test]
        public void CatalogListViewItemActivate_Callback__GoToDirectoryRoot_On_Same_RootEntry_Does_Not_Set_Root()
        {
            var testRootTreeNode = new TreeNode("Moo") { Tag = _rootEntry };
            _mockForm.Stub(x => x.DirectoryTreeViewNodes)
                .Repeat.Times(1)    // enforcing repeat makes this a fragile test ?
                .Return(testRootTreeNode);

            // verify does not set DirectoryTreeViewNodes
            _mockForm.Stub(x => x.DirectoryTreeViewNodes = Arg<TreeNode>.Is.Anything)
                .WhenCalled(a => Assert.Fail("GoToDirectoryRoot tried to set DirectoryTreeViewNodes"));

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with CatalogListView our activated item is a RootEntry.
            _sutPresenter.CatalogListViewItemActivate();
            var action = GetPresenterAction(_stubCatalogListViewHelper);
            action(_rootEntry);

            _mockForm.VerifyAllExpectations();
        }

        [Test]
        public void CatalogListViewItemActivate_Callback__GoToDirectoryRoot_On_Different_RootEntry_Sets_New_Root()
        {
            var alternateRootEntry = new RootEntry { Path = "alternate" };
            var testRootTreeNode = new TreeNode("Moo") { Tag = alternateRootEntry };
            _mockForm.Stub(x => x.DirectoryTreeViewNodes)
                .Repeat.Times(1) // enforcing repeat makes this a fragile test ?
                .Return(testRootTreeNode);

            // verify does set DirectoryTreeViewNodes
            _mockForm.Stub(x => x.DirectoryTreeViewNodes = Arg<TreeNode>.Is.Anything)
                .Repeat.Times(1);

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with CatalogListView our activated item is a RootEntry.
            _sutPresenter.CatalogListViewItemActivate();
            var action = GetPresenterAction(_stubCatalogListViewHelper);
            action(_rootEntry);

            _mockForm.VerifyAllExpectations();
        }

        [Test]
        public void CatalogListViewItemActivate_Callback__GoToDirectoryRoot_On_Null_RootNode_Sets_New_Root()
        {
            // verify does set DirectoryTreeViewNodes
            _mockForm.Stub(x => x.DirectoryTreeViewNodes = Arg<TreeNode>.Is.Anything)
                .Repeat.Times(1);

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with CatalogListView our activated item is a RootEntry.
            _sutPresenter.CatalogListViewItemActivate();
            var action = GetPresenterAction(_stubCatalogListViewHelper);
            action(_rootEntry);

            _mockForm.VerifyAllExpectations();
        }

        [Test]
        public void CatalogListViewItemActivate_Callback__GoToDirectoryRoot_Sets_Directory_Pane()
        {
            // Go to Directory pane
            _mockForm.Stub(x => x.SelectDirectoryPane())
                .Repeat.Times(1);

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with CatalogListView our activated item is a RootEntry.
            _sutPresenter.CatalogListViewItemActivate();
            var action = GetPresenterAction(_stubCatalogListViewHelper);
            action(_rootEntry);

            _mockForm.VerifyAllExpectations();
        }

        [Test]
        public void CatalogListViewItemActivate_Callback__GoToDirectoryRoot_On_Null_RootNode_Calls_InitSort()
        {
            // Initialise Sort on ListView when Root Set.
            _mockDirectoryListViewHelper.Stub(x => x.InitSort())
                .Repeat.Times(1);

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with CatalogListView our activated item is a RootEntry.
            _sutPresenter.CatalogListViewItemActivate();
            var action = GetPresenterAction(_stubCatalogListViewHelper);
            action(_rootEntry);

            _mockForm.VerifyAllExpectations();
            _mockDirectoryListViewHelper.VerifyAllExpectations();
        }
    }
    // ReSharper restore InconsistentNaming
}

//namespace Test.Fohjin.DDD.Scenarios.Opening_the_bank_application
//{
//    public class When_in_the_GUI_openeing_the_bank_application : PresenterTestFixture<ClientSearchFormPresenter>
//    {
//        private List<ClientReport> _clientReports;

//        protected override void SetupDependencies()
//        {
//            _clientReports = new List<ClientReport> { new ClientReport(Guid.NewGuid(), "Client Name") };
//            OnDependency<IReportingRepository>()
//                .Setup(x => x.GetByExample<ClientReport>(null))
//                .Returns(_clientReports);
//        }

//        protected override void When()
//        {
//            Presenter.Display();
//        }

//        [Then]
//        public void Then_show_dialog_will_be_called_on_the_view()
//        {
//            On<IClientSearchFormView>().VerifyThat.Method(x => x.ShowDialog()).WasCalled();
//        }

//        [Then]
//        public void Then_client_report_data_from_the_reporting_repository_is_being_loaded_into_the_view()
//        {
//            On<IClientSearchFormView>().VerifyThat.ValueIsSetFor(x => x.Clients = _clientReports);
//        }
//    }
//}

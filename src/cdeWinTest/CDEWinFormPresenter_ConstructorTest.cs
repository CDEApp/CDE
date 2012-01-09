using System;
using System.Collections.Generic;
using System.Linq;
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
        private static string ChildrenPendingDummyNodeName = "_dummyNode";
        private CDEWinFormPresenter _sutPresenter;

        [SetUp]
        override public void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();
            _sutPresenter = new CDEWinFormPresenter(_mockForm, RootEntryTestSource.RootList, _stubConfig);
        }

        [Ignore("This cant really happen in a real TreeView, as the event to be triggered means there is a node")]
        [ExpectedException(typeof(NullReferenceException))]
        [Test]
        public void DirectoryTreeViewBeforeExpandNode_WIthTreeViewRoot_Null_Throws_Exception()
        {
            _mockForm.Stub(x => x.DirectoryTreeViewActiveBeforeExpandNode)
                .Return(null);

            _sutPresenter.DirectoryTreeViewBeforeExpandNode();
        }

        [Test]
        public void DirectoryTreeViewBeforeExpandNode_With_Out_Dummy_Child_Does_Nothing()
        {
            var testTreeNode = new TreeNode("testTreeNode");
            _mockForm.Stub(x => x.DirectoryTreeViewActiveBeforeExpandNode)
                .Return(testTreeNode);

            _sutPresenter.DirectoryTreeViewBeforeExpandNode();

            Assert.That(testTreeNode.Nodes.Count, Is.EqualTo(0), "It appears child nodes were added thats wrong for a test node with no dummy children marking children needed.");
            Assert.That(testTreeNode.Text, Is.EqualTo("testTreeNode"), "Node text changed values thats wrong.");
        }

        [Test]
        public void DirectoryTreeViewBeforeExpandNode_With_Dummy_Child_Replaces_Dummy_With_Children()
        {
            InitRootWithDir();
            var testTreeNode = new TreeNode("testTreeNode");
            testTreeNode.Tag = _rootEntry; // require a pointer to valid DirEntry which has its own children.
            var dummyChildNode = new TreeNode(ChildrenPendingDummyNodeName);
            testTreeNode.Nodes.Add(dummyChildNode);

            _mockForm.Stub(x => x.DirectoryTreeViewActiveBeforeExpandNode)
                .Return(testTreeNode);

            _sutPresenter.DirectoryTreeViewBeforeExpandNode();

            Assert.That(testTreeNode.Nodes.Count, Is.EqualTo(1), "It appears a child node was not added.");
            var child = testTreeNode.Nodes[0];
            Assert.That(child.Text, Is.EqualTo("Test1"), "Dummy child node was not converted to child of our CommonEntry.");
            Assert.That(child.Nodes.Count, Is.EqualTo(0), "A CommonEntry with no children appears to have had one added.");
        }

        [Test]
        public void DirectoryTreeViewBeforeExpandNode_Dummy_Converted_To_Children_Files_Not_Added_To_TreeView()
        {
            InitRootWithDirDirFileWithDir();
            var testTreeNode = new TreeNode("testTreeNode");
            testTreeNode.Tag = _rootEntry; // require a pointer to valid DirEntry with children.
            var dummyChildNode = new TreeNode(ChildrenPendingDummyNodeName);
            testTreeNode.Nodes.Add(dummyChildNode);

            _mockForm.Stub(x => x.DirectoryTreeViewActiveBeforeExpandNode)
                .Return(testTreeNode);

            _sutPresenter.DirectoryTreeViewBeforeExpandNode();

            Assert.That(testTreeNode.Nodes.Count, Is.EqualTo(2), "Two child nodes were expected one for each Entry that is a directory.");
            var child1 = testTreeNode.Nodes[0];
            var child2 = testTreeNode.Nodes[1];
            Assert.That(child1.Text, Is.EqualTo("Test1"), "First Child has not got expected name.");
            Assert.That(child2.Text, Is.EqualTo("Test3"), "Second Child has not got expected name.");
            var childOfChild = child1.Nodes[0];
            Assert.That(childOfChild.Text, Is.EqualTo(ChildrenPendingDummyNodeName), "New Child with children did not get dummy child created for its Children.");
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
        public void SearchResultListViewItemActivate_Callback__ViewFileInDirectoryTab_With_File_OK()
        {
            Object treeViewRootNodeArg = null;
            Object listViewListArg = null;
            InitRootWithFile();

            // Directory TreeView gets a new root node, since there is no previous one loaded.
            _mockForm.Stub(x => x.DirectoryTreeViewNodes = Arg<TreeNode>.Is.Anything)
                .Repeat.Times(1)
                .WhenCalled(a => treeViewRootNodeArg = a.Arguments[0]);

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
        public void SearchResultListViewItemActivate_Callback__ViewFileInDirectoryTab__With_File_On_Same_RootEntry_Does_Not_Set_Root()
        {
            InitRootWithFile();

            var testRootTreeNode = new TreeNode("Moo") { Tag = _rootEntry };
            _mockForm.Stub(x => x.DirectoryTreeViewNodes)
                .Repeat.Times(1)    // enforcing repeat makes this a fragile test ?
                .Return(testRootTreeNode);

            // verify does not set DirectoryTreeViewNodes
            _mockForm.Stub(x => x.DirectoryTreeViewNodes = Arg<TreeNode>.Is.Anything)
                .WhenCalled(a => Assert.Fail("GoToDirectoryRoot tried to set DirectoryTreeViewNodes"));

            MockTreeViewAfterSelect(_sutPresenter);

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with SearchResultListView our activated item is a PairDirEntry.
            _sutPresenter.SearchResultListViewItemActivate();
            var action = GetPresenterAction(_stubSearchResultListViewHelper);
            action(_pairDirEntry);

            _mockForm.VerifyAllExpectations();
        }

        [Test]
        public void SearchResultListViewItemActivate_Callback__ViewFileInDirectoryTab__With_Directory_Does_Not_Select_File_In_ListView()
        {
            InitRootWithDir();
            //MockTreeViewAfterSelect(_sutPresenter); // as we dont try to select file we dont need to mock AfterSelect behaviour.

            // Select the item in list view
            _mockDirectoryListViewHelper.Stub(x => x.SelectItem(Arg<int>.Is.Anything))
                .WhenCalled(a => Assert.Fail("Select Item was called on ListView for viewin a File Entry in Directory Tab."));

            // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
            // with SearchResultListView our activated item is a PairDirEntry.
            _sutPresenter.SearchResultListViewItemActivate();
            var action = GetPresenterAction(_stubSearchResultListViewHelper);
            action(_pairDirEntry);
        }

        protected TreeNode _treeViewAfterSelectNode;
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
            InitRootWithFile();
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
            var action = GetPresenterAction(_mockCatalogListViewHelper);
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
            var action = GetPresenterAction(_mockCatalogListViewHelper);
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
            var action = GetPresenterAction(_mockCatalogListViewHelper);
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
            var action = GetPresenterAction(_mockCatalogListViewHelper);
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
            var action = GetPresenterAction(_mockCatalogListViewHelper);
            action(_rootEntry);

            _mockForm.VerifyAllExpectations();
            _mockDirectoryListViewHelper.VerifyAllExpectations();
        }
    }
    // ReSharper restore InconsistentNaming


    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class CDEWinFormPresenter_CatalogRetrieveVirtualItem : TestCDEWinPresenterBase
    {
        private CDEWinFormPresenter _sutPresenter;

        [SetUp]
        public override void RunBeforeEveryTest()
        {
            base.RunBeforeEveryTest();

            _stubConfig.Stub(x => x.DefaultCatalogColumnCount)
                .Return(12); // enough spaces for catalog list view items.
            InitRootWithFile();
            _sutPresenter = new CDEWinFormPresenter(_mockForm, new List<RootEntry> { _rootEntry }, _stubConfig);
            InitRootWithFile();
        }

        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [Test]
        public void CatalogRetrieveVirtualItem_Of_Invalid_ItemIndex_KABOOM()
        {
            _mockCatalogListViewHelper.Stub(x => x.RetrieveItemIndex)
                .Return(1);

            _sutPresenter.CatalogRetrieveVirtualItem();
        }

        [Test]
        public void CatalogRetrieveVirtualItem_Produces_ListView_Field()
        {
            // dont forget there is a ToLocalTime() call in catalog rendering....
            _mockCatalogListViewHelper.Stub(x => x.RetrieveItemIndex)
                .Return(0);

            _sutPresenter.CatalogRetrieveVirtualItem();

            var args = _mockCatalogListViewHelper
                .GetArgumentsForCallsMadeOn(x => x.RenderItem = Arg<ListViewItem>.Is.Anything);
            var listViewItem = (ListViewItem)(args[0][0]);

            var expectedValues = new []
                {   
                    @"T:\","TestVolume","0","1","1",
                    "Z","736.6 KB","639 KB",
                    "2011/12/02 03:15:13",  // Fragile, this depends on time zone of test machine at moment.
                    "TestRootEntry.cde",
                    @"C:\Users\testuser\AppData\Local\cde",
                    "Test Root Entry Description"
                };

            for (var i = 0; i < expectedValues.Length; i++)
            {
                var expect = expectedValues[i];
                var got = listViewItem.SubItems[i].Text;
                if (expect != got)
                {
                    Assert.Fail("At index {0} expected item \"{1}\" did not match \"{2}\"", i, expect, got);
                }
            }
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

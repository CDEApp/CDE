using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NUnit.Framework;
using Rhino.Mocks;
using Util;
using cdeLib;
using cdeWin;
using Is = NUnit.Framework.Is;

//using Is = NUnit.Framework.Is;

//using Is = NUnit.Framework.Is;

namespace cdeWinTest
{
    public class CDEWinFormPresenterTest
    {
        // ReSharper disable InconsistentNaming
        [TestFixture]
        public class ConstructorTest : TestCDEWinPresenterBase
        {
            [SetUp]
            override public void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
            }

            [Test]
            public void Always_Set_Search_Button()
            {
                new CDEWinFormPresenter(_mockForm, _stubConfig);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SearchButtonText = Arg<string>.Is.Anything);
                var comparisonParam = args.Count > 0 ? (string)(args[0][0]) : "ValueWasNotSetItAppears";
                Assert.That(comparisonParam, Is.EqualTo("Search"), "SearchButtonText is not the expected \"Search\".");
            }

            [Test]
            public void Always_Catalog_SortList()
            {
                new CDEWinFormPresenter(_mockForm, _stubConfig);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SortList(Arg<IListViewHelper<RootEntry>>.Is.Anything));
                var comparisonParam = args.Count > 0 ? (IListViewHelper<RootEntry>)(args[0][0]) : null;
                Assert.That(comparisonParam, Is.Not.Null, "SortList for catalog does not appear to be set.");
            }

            [Test]
            public void Always_Register_SearchResult_Sorter()
            {
                new CDEWinFormPresenter(_mockForm, _stubConfig);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SetColumnSortCompare(
                    Arg<ListViewHelper<PairDirEntry>>.Is.Anything, Arg<Comparison<PairDirEntry>>.Is.Anything));
                var comparisonParam = args.Count > 0 ? (Comparison<PairDirEntry>)(args[0][1]) : null;
                Assert.That(comparisonParam, Is.Not.Null, "Comparison for SearchResultListViewHelper was not set.");
            }

            [Test]
            public void Always_Register_Catalog_Sorter()
            {
                new CDEWinFormPresenter(_mockForm, _stubConfig);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SetColumnSortCompare(
                    Arg<ListViewHelper<RootEntry>>.Is.Anything, Arg<Comparison<RootEntry>>.Is.Anything));
                var comparisonParam = args.Count > 0 ? (Comparison<RootEntry>)(args[0][1]) : null;
                Assert.That(comparisonParam, Is.Not.Null, "Comparison for CatalogListViewHelper was not set.");
            }

            [Test]
            public void Always_Register_Directory_Sorter()
            {
                new CDEWinFormPresenter(_mockForm, _stubConfig);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SetColumnSortCompare(
                    Arg<ListViewHelper<DirEntry>>.Is.Anything, Arg<Comparison<DirEntry>>.Is.Anything));
                var comparisonParam = args.Count > 0 ? (Comparison<DirEntry>)(args[0][1]) : null;
                Assert.That(comparisonParam, Is.Not.Null, "Comparison for DirectoryListViewHelper was not set.");
            }

            [Ignore("This test is not a valid scenario for production, as null root entry list should not happen.")]
            [Test]
            public void With_Null_RootEntry_List()
            {
                _mockForm.Stub(x => x.SetList(Arg<ListViewHelper<RootEntry>>.Is.Anything, Arg<List<RootEntry>>.Is.Same(null)))
                    .Return(3);

                new CDEWinFormPresenter(_mockForm, _stubConfig);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SetCatalogsLoadedStatus(Arg<int>.Is.Anything));
                var comparisonParam = args.Count > 0 ? (int)(args[0][0]) : -1;
                Assert.That(comparisonParam, Is.EqualTo(3), "SetCatalogsLoadedStatus expected same as result of SetList 3.");
            }

            [Test]
            public void With_Empty_RootEntry_List()
            {
                _mockForm.Stub(x => x.SetList(Arg<ListViewHelper<RootEntry>>.Is.Anything, Arg<List<RootEntry>>.Is.Same(_emptyRootList)))
                    .Return(0);

                new CDEWinFormPresenter(_mockForm, _stubConfig);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SetCatalogsLoadedStatus(Arg<int>.Is.Anything));
                var comparisonParam = args.Count > 0 ? (int)(args[0][0]) : -1;
                Assert.That(comparisonParam, Is.EqualTo(0), "SetCatalogsLoadedStatus expected same as result of SetList 0.");
            }

            [Test]
            public void With_RootEntry_List()
            {
                InitRootWithDir();
                _mockForm.Stub(x => x.SetList(Arg<ListViewHelper<RootEntry>>.Is.Anything, Arg<List<RootEntry>>.Is.Same(_rootList)))
                    .Return(1);

                var _loadCatalogsService = MockRepository.GenerateStub<ILoadCatalogService>();
                _loadCatalogsService.Stub(x => x.LoadRootEntries(Arg<IConfig>.Is.Anything, Arg<TimeIt>.Is.Anything))
                    .Return(_rootList);

                new CDEWinFormPresenter(_mockForm, _stubConfig, _loadCatalogsService);

                var args = _mockForm.GetArgumentsForCallsMadeOn(x => x.SetCatalogsLoadedStatus(Arg<int>.Is.Anything));
                var comparisonParam = args.Count > 0 ? (int)(args[0][0]) : -1;
                Assert.That(comparisonParam, Is.EqualTo(1), "SetCatalogsLoadedStatus expected same as result of SetList 1.");
            }
        }

        [TestFixture]
        public class DirectoryTreeViewBeforeExpandNode : TestCDEWinPresenterBase
        {
            private const string ChildrenPendingDummyNodeName = "_dummyNode";
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            override public void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
                InitRootWithDir();
				_sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, null);
            }

            [Ignore("This cant really happen in a real TreeView, as the event to be triggered means there is a node")]
            [ExpectedException(typeof(NullReferenceException))]
            [Test]
            public void With_TreeViewRoot_Null_Throws_Exception()
            {
                _mockForm.Stub(x => x.DirectoryTreeViewActiveBeforeExpandNode)
                    .Return(null);

                _sutPresenter.DirectoryTreeViewBeforeExpandNode();
            }

            [Test]
            public void With_Out_Dummy_Child_Does_Nothing()
            {
                var testTreeNode = new TreeNode("testTreeNode");
                _mockForm.Stub(x => x.DirectoryTreeViewActiveBeforeExpandNode)
                    .Return(testTreeNode);

                _sutPresenter.DirectoryTreeViewBeforeExpandNode();

                Assert.That(testTreeNode.Nodes.Count, Is.EqualTo(0), "It appears child nodes were added thats wrong for a test node with no dummy children marking children needed.");
                Assert.That(testTreeNode.Text, Is.EqualTo("testTreeNode"), "Node text changed values thats wrong.");
            }

            [Test]
            public void With_Dummy_Child_Replaces_Dummy_With_Children()
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
            public void Dummy_Converted_To_Children_Files_Not_Added_To_TreeView()
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

        [TestFixture]
        public class SearchResultListViewItemActivate : TestCDEWinPresenterBase
        {
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            override public void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
                InitRootWithDir();
				_sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, null);
            }

            [Test]
            public void Callback_ViewFileInDirectoryTab_With_File()
            {
                // Hmmm this is asserting a lot in one [Test].
                // - break it up into multiple tests 1 assert each ?
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

                //TracePresenterAction(_mockSearchResultListViewHelper);

                // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
                // with SearchResultListView our activated item is a PairDirEntry.
                _sutPresenter.SearchResultListViewItemActivate();
                var action = GetPresenterAction(_mockSearchResultListViewHelper);
                action(_pairDirEntry);

                _mockDirectoryListViewHelper.VerifyAllExpectations();
                _mockForm.VerifyAllExpectations();
                Assert.That(_treeViewAfterSelectNode.Text, Is.EqualTo(@"T:\"), "ListView selected node text value not expected.");

                var treeViewRootNode = (TreeNode)treeViewRootNodeArg;
                Assert.That(treeViewRootNode.Text, Is.EqualTo(@"T:\"), "TreeView root node Text value not expected");

                var list = (List<DirEntry>)listViewListArg;
                Assert.That(list.Count, Is.EqualTo(1), "The list set on ListViewHelper wasnt expected");
                Assert.That(list[0].Path, Is.EqualTo("Test"), "The list set on ListViewHelper wasnt expected");
            }

            [Test]
            public void Callback_ViewFileInDirectoryTab_With_File_On_Same_RootEntry_Does_Not_Set_Root()
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
                var action = GetPresenterAction(_mockSearchResultListViewHelper);
                action(_pairDirEntry);

                _mockForm.VerifyAllExpectations();
            }

            [Test]
            public void Callback_ViewFileInDirectoryTab_With_Directory_Does_Not_Select_File_In_ListView()
            {
                InitRootWithDir();
                //MockTreeViewAfterSelect(_sutPresenter); // as we dont try to select file we dont need to mock AfterSelect behaviour.

                // Select the item in list view
                _mockDirectoryListViewHelper.Stub(x => x.SelectItem(Arg<int>.Is.Anything))
                    .WhenCalled(a => Assert.Fail("Select Item was called on ListView for viewin a File Entry in Directory Tab."));

                // ACTIVATE public method.... due to call back nature the action() call below is the actual operation.
                // with SearchResultListView our activated item is a PairDirEntry.
                _sutPresenter.SearchResultListViewItemActivate();
                var action = GetPresenterAction(_mockSearchResultListViewHelper);
                action(_pairDirEntry);
            }
        }

        [TestFixture]
        public class CatalogListViewItemActivate : TestCDEWinPresenterBase
        {
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
                InitRootWithDir();
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig);
                InitRootWithFile();
            }

            [Test]
            public void Callback_GoToDirectoryRoot_On_Same_RootEntry_Does_Not_Set_Root()
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
            public void Callback_GoToDirectoryRoot_On_Different_RootEntry_Sets_New_Root()
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
            public void Callback_GoToDirectoryRoot_On_Null_RootNode_Sets_New_Root()
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
            public void Callback_GoToDirectoryRoot_Sets_Directory_Pane()
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
            public void Callback_GoToDirectoryRoot_On_Null_RootNode_Calls_InitSort()
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

        [TestFixture]
        public class CatalogRetrieveVirtualItem : TestCDEWinPresenterBase
        {
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();

                _stubConfig.Stub(x => x.DefaultCatalogColumnCount)
                    .Return(13); // enough spaces for catalog list view items.
                _stubConfig.Stub(x => x.DateFormatYMDHMS)
                    .Return("{0:yyyy/MM/dd HH:mm:ss}");

                InitRootWithFile();

                var _loadCatalogsService = MockRepository.GenerateStub<ILoadCatalogService>();
                _loadCatalogsService.Stub(x => x.LoadRootEntries(Arg<IConfig>.Is.Anything, Arg<TimeIt>.Is.Anything))
                    .Return(_rootList);

                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, _loadCatalogsService);
            }

            /// <summary>
            /// This is not something that should happen as listview wont ask for 
            /// an Item Index that is outside bounds of the setup ListView.
            /// </summary>
            [ExpectedException(typeof(ArgumentOutOfRangeException))]
            [Test]
            public void Invalid_ItemIndex_Throws_Exception()
            {
                _mockCatalogListViewHelper.Stub(x => x.RetrieveItemIndex)
                    .Return(1);

                _sutPresenter.CatalogRetrieveVirtualItem();
            }

            [Test]
            public void Produces_ListView_Field()
            {
                // dont forget there is a ToLocalTime() call in catalog rendering....
                _mockCatalogListViewHelper.Stub(x => x.RetrieveItemIndex)
                    .Return(0);

                _sutPresenter.CatalogRetrieveVirtualItem();

                var args = _mockCatalogListViewHelper
                    .GetArgumentsForCallsMadeOn(x => x.RenderItem = Arg<ListViewItem>.Is.Anything);
                var listViewItem = (ListViewItem)(args[0][0]);
                var expectedValues = new[]
                {   
                    @"T:\","TestVolume","0","1","1",
                    "Z","531 B","736.6 KB","639 KB",
                    "2011/12/02 03:15:13",  // Fragile, this depends on time zone of test machine at moment.
                    "34 msec",
                    @".\TestRootEntry.cde",
                    "Test Root Entry Description"
                };
                listViewItem.AssertListViewSubItemEqualValues(expectedValues);
            }
        }

        [TestFixture]
        public class SearchResultRetrieveVirtualItem : TestCDEWinPresenterBase
        {
            private TestPresenterSetSearch _sutPresenter;

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();

                _stubConfig.Stub(x => x.DefaultSearchResultColumnCount)
                    .Return(4); // enough spaces for search result list view items.
                _stubConfig.Stub(x => x.DateFormatYMDHMS)
                    .Return("{0:yyyy/MM/dd HH:mm:ss}");

                InitRootWithFile();
                _sutPresenter = new TestPresenterSetSearch(_mockForm, _stubConfig);
            }

            [Test]
            public void Nothing_Bombs_If_No_ListViewEntries()
            {
                _sutPresenter.TestSetSearchResultList(null);

                _sutPresenter.SearchResultRetrieveVirtualItem();
            }

            [Test]
            public void Nothing_Bombs_With_List_Empty_Does_Nothing()
            {
                var pairDirList = new List<PairDirEntry>();
                _sutPresenter.TestSetSearchResultList(pairDirList);

                _sutPresenter.SearchResultRetrieveVirtualItem();
            }

            /// <summary>
            /// This is not something that should happen as listview wont ask for 
            /// an Item Index that is outside bounds of the setup ListView.
            /// </summary>
            [ExpectedException(typeof(ArgumentOutOfRangeException))]
            [Test]
            public void Invalid_ItemIndex_With_List_Wrong_Index_Throws_Exception()
            {
                var pairDirList = new List<PairDirEntry> { _pairDirEntry };
                _sutPresenter.TestSetSearchResultList(pairDirList);

                _mockSearchResultListViewHelper.Stub(x => x.RetrieveItemIndex)
                    .Return(1);

                _sutPresenter.SearchResultRetrieveVirtualItem();
            }

            [Test]
            public void Valid_DirEntry()
            {
                var pairDirList = new List<PairDirEntry> { _pairDirEntry };
                _sutPresenter.TestSetSearchResultList(pairDirList);
                _mockSearchResultListViewHelper.Stub(x => x.RetrieveItemIndex)
                    .Return(0);

                _sutPresenter.SearchResultRetrieveVirtualItem();

                var args = _mockSearchResultListViewHelper
                    .GetArgumentsForCallsMadeOn(x => x.RenderItem = Arg<ListViewItem>.Is.Anything);
                var listViewItem = (ListViewItem)(args[0][0]);
                var expectedValues = new[]
                {   // Fragile, this depends on time zone of test machine at moment.
                    "Test", "531", "2010/11/02 18:16:12", @"T:\"
                };
                listViewItem.AssertListViewSubItemEqualValues(expectedValues);
            }

            public class TestPresenterSetSearch : CDEWinFormPresenter
            {
                public TestPresenterSetSearch(ICDEWinForm form, IConfig config): base(form, config, null)
                {
                }

                public int TestSetSearchResultList(List<PairDirEntry> list)
                {
                    return SetSearchResultList(list);
                }
            }
        }

        [TestFixture]
        public class DirectoryTreeViewAfterSelect : TestCDEWinPresenterBase
        {
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
                InitRootWithFile();
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, null);
            }

            [Test]
            public void Set_ListView_List()
            {
                var testRootTreeNode = new TreeNode("MyTestNode") {Tag = _rootEntry};
                _mockForm.Stub(x => x.DirectoryTreeViewActiveAfterSelectNode)
                    .Return(testRootTreeNode);

                // Directory ListView gets children of root
                object listViewListArg = null;
                _mockDirectoryListViewHelper.Stub(x => x.SetList(Arg<List<DirEntry>>.Is.Anything))
                    .Return(1)
                    .WhenCalled(a => listViewListArg = a.Arguments[0]);

                _sutPresenter.DirectoryTreeViewAfterSelect();

                var list = (List<DirEntry>) listViewListArg;
                Assert.That(list.Count, Is.EqualTo(1), "The list set on ListViewHelper wasnt expected");
                Assert.That(list[0].Path, Is.EqualTo("Test"), "The list set on ListViewHelper wasnt expected");

                var args = _mockForm
                    .GetArgumentsForCallsMadeOn(x => x.SetDirectoryPathTextbox = Arg<string>.Is.Anything);
                var pathTextBoxValue = (string) (args[0][0]);

                Assert.That(pathTextBoxValue, Is.EqualTo(@"T:\"));
            }
        }

        [TestFixture]
        public class DirectoryRetrieveVirtualItem : TestCDEWinPresenterBase
        {
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();

                _stubConfig.Stub(x => x.DefaultDirectoryColumnCount)
                    .Return(3); // enough spaces for directory list view items.
                InitRootWithFile();
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, null);
            }

            [Test]
            public void Nothing_Bombs_If_No_ListViewEntries()
            {
                //_sutPresenter.TestSetSearchResultList(null);

                _sutPresenter.DirectoryRetrieveVirtualItem();
            }

            [Test]
            public void Nothing_Bombs_With_List_Empty_Does_Nothing()
            {
                //_sutPresenter.TestSetSearchResultList(null);
                //
                MockTreeViewAfterSelect(_sutPresenter);

                _sutPresenter.DirectoryRetrieveVirtualItem();
            }
        }

        // ReSharper restore InconsistentNaming
    }

    public static class ListViewTestExtension
    {
        public static void AssertListViewSubItemEqualValues(this ListViewItem listViewItem, string[] expectedValues)
        {
            for (var i = 0; i < expectedValues.Length; i++)
            {
                var expect = expectedValues[i];
                var got = listViewItem.SubItems[i].Text;
                if (expect != got)
                {
                    Assert.Fail("At index {0} expected item \"{1}\" did not match \"{2}\"", i, expect, got);
                }
                else
                {
                    Console.WriteLine("{0} {1}", i, expect);
                }
            }
        }
    }
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

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using cdeLib;
using cdeLib.Infrastructure.Config;
using cdeWin;
using JetBrains.Annotations;
using NUnit.Framework;
using Util;
using NSubstitute;

namespace cdeWinTest
{
    [UsedImplicitly]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1102:Make class static.", Justification = "<Pending>")]
    public class CDEWinFormPresenterTest
    {
        // ReSharper disable InconsistentNaming
        [TestFixture]
        public class ConstructorTest : TestCDEWinPresenterBase
        {
            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
            }

            [Test]
            public void Always_Set_Search_Button()
            {
                var _ = new CDEWinFormPresenter(_mockForm, _stubConfig);

                _mockForm.Received().SearchButtonText = "Search";
            }

            [Test]
            public void Always_Catalog_SortList()
            {
                var _ = new CDEWinFormPresenter(_mockForm, _stubConfig);

                // It's null because we have no fake setup for _rootEntries configured with fakes.
                _mockCatalogListViewHelper.Received().SetList(null);
            }

            [Test]
            public void Always_Register_Result_Sorters()
            {
                var _ = new CDEWinFormPresenter(_mockForm, _stubConfig);

                _mockSearchResultListViewHelper.ColumnSortCompare = Arg.Any<Comparison<PairDirEntry>>();
                _mockCatalogListViewHelper.ColumnSortCompare = Arg.Any<Comparison<RootEntry>>();
                _mockDirectoryListViewHelper.ColumnSortCompare = Arg.Any<Comparison<ICommonEntry>>();
            }

            [Test]
            public void With_Null_RootEntry_List_SetsCatalogsLoaded()
            {
                // null should not happen at runtime but its ok for this test.
                _mockCatalogListViewHelper.SetList(null).Returns(3);

                var _ = new CDEWinFormPresenter(_mockForm, _stubConfig);

                _mockForm.Received().SetCatalogsLoadedStatus(3);
            }
        }

        [TestFixture]
        public class DirectoryTreeViewBeforeExpandNode : TestCDEWinPresenterBase
        {
            private const string ChildrenPendingDummyNodeName = "_dummyNode";
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
                InitRootWithDir();
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, null);
            }

            [Test]
            public void With_RootEntry_List_SetsTotalFileEntries()
            {
                var _loadCatalogsService = Substitute.For<ILoadCatalogService>();
                _loadCatalogsService
                    .LoadRootEntries(Arg.Any<IConfig>(), Arg.Any<TimeIt>())
                    .Returns(_rootList);

                var _ = new CDEWinFormPresenter(_mockForm, _stubConfig, _loadCatalogsService);

                _mockForm.Received().SetTotalFileEntriesLoadedStatus(1);
            }

            [Ignore("This cant really happen in a real TreeView, as the event to be triggered means there is a node")]
            [Test]
            public void With_TreeViewRoot_Null_Throws_Exception()
            {
                _mockForm.DirectoryTreeViewActiveBeforeExpandNode.Returns(null as TreeNode);

                Assert.Throws<NullReferenceException>(() => _sutPresenter.DirectoryTreeViewBeforeExpandNode());
            }

            [Test]
            public void With_Out_Dummy_Child_Does_Nothing()
            {
                var testTreeNode = new TreeNode("testTreeNode");
                _mockForm.DirectoryTreeViewActiveBeforeExpandNode.Returns(testTreeNode);

                _sutPresenter.DirectoryTreeViewBeforeExpandNode();

                Assert.That(testTreeNode.Nodes.Count, Is.EqualTo(0),
                    "It appears child nodes were added that's wrong for a test node with no dummy children marking children needed.");
                Assert.That(testTreeNode.Text, Is.EqualTo("testTreeNode"), "Node text changed values that's wrong.");
            }

            [Test]
            public void With_Dummy_Child_Replaces_Dummy_With_Children()
            {
                var testTreeNode = new TreeNode("testTreeNode") { Tag = _rootEntry };
                // require a pointer to valid DirEntry which has its own children.
                var dummyChildNode = new TreeNode(ChildrenPendingDummyNodeName);
                testTreeNode.Nodes.Add(dummyChildNode);
                _mockForm.DirectoryTreeViewActiveBeforeExpandNode.Returns(testTreeNode);

                _sutPresenter.DirectoryTreeViewBeforeExpandNode();

                Assert.That(testTreeNode.Nodes.Count, Is.EqualTo(1), "It appears a child node was not added.");
                var child = testTreeNode.Nodes[0];
                Assert.That(child.Text, Is.EqualTo("Test1"),
                    "Dummy child node was not converted to child of our CommonEntry.");
                Assert.That(child.Nodes.Count, Is.EqualTo(0),
                    "A CommonEntry with no children appears to have had one added.");
            }

            [Test]
            public void Dummy_Converted_To_Children_Files_Not_Added_To_TreeView()
            {
                InitRootWithDirDirFileWithDir();
                var testTreeNode = new TreeNode("testTreeNode") { Tag = _rootEntry }; // require a pointer to valid DirEntry with children.
                var dummyChildNode = new TreeNode(ChildrenPendingDummyNodeName);
                testTreeNode.Nodes.Add(dummyChildNode);
                _mockForm.DirectoryTreeViewActiveBeforeExpandNode.Returns(testTreeNode);

                _sutPresenter.DirectoryTreeViewBeforeExpandNode();

                Assert.That(testTreeNode.Nodes.Count, Is.EqualTo(2),
                    "Two child nodes were expected one for each Entry that is a directory.");
                var child1 = testTreeNode.Nodes[0];
                var child2 = testTreeNode.Nodes[1];
                Assert.That(child1.Text, Is.EqualTo("Test1"), "First Child has not got expected name.");
                Assert.That(child2.Text, Is.EqualTo("Test3"), "Second Child has not got expected name.");
                var childOfChild = child1.Nodes[0];
                Assert.That(childOfChild.Text, Is.EqualTo(ChildrenPendingDummyNodeName),
                    "New Child with children did not get dummy child created for its Children.");
            }
        }

        [TestFixture]
        public class SearchResultListViewItemActivate : TestCDEWinPresenterBase
        {
            private CDEWinFormPresenter _sutPresenter;

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                base.RunBeforeEveryTest();
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, null);
            }

            //////////////////////////////////////////////////////////////////////////////////////////
            // REMINDER ListViewHelper AfterActivateIndex
            //  - is read by SearchResultListViewItemActivate for what is activated.
            //  But this is short circuited by setup of which passes the PairDirEntry directly
            //  - _mockSearchResultListViewHelper.ActionOnActivateItem.
            //////////////////////////////////////////////////////////////////////////////////////////

            [Test]
            public void Search_Activate_File_Item_Sets_TreeView_Sets_FileListView()
            {
                InitRootWithFile();
                TreeNode treeViewSelectedNode = null;
                MockDirectoryTreeViewAfterSelect(_sutPresenter, x => treeViewSelectedNode = x);
                TreeNode treeViewRootNode = null;
                _mockForm.DirectoryTreeViewNodes = Arg.Do<TreeNode>(x => treeViewRootNode = x);
                List<ICommonEntry> listViewListArg = null;
                _mockDirectoryListViewHelper.SetList(Arg.Do<List<ICommonEntry>>(x => listViewListArg = x));
                FakeItemActivateWithValue(_mockSearchResultListViewHelper, _pairDirEntry);

                // ACT
                _sutPresenter.SearchResultListViewItemActivate();

                _mockForm.Received(1).DirectoryTreeViewNodes = Arg.Any<TreeNode>();
                Assert.That(treeViewSelectedNode.Text, Is.EqualTo(@"T:\"),
                    "ListView selected node text value not expected.");
                Assert.That(treeViewRootNode.Text, Is.EqualTo(@"T:\"), "TreeView root node Text value not expected");
                Assert.That(listViewListArg.Count, Is.EqualTo(1), "The list set on ListViewHelper wasn't expected");
                Assert.That(listViewListArg[0].Path, Is.EqualTo("Test"),
                    "The list set on ListViewHelper wasn't expected");
            }

            // Show RootNode in TreeView and File in File view 
            [Test]
            public void Search_Activate_Dir_Item_Sets_TreeView_Empty_FileListView()
            {
                InitRootWithDir();
                TreeNode treeViewSelectedNode = null;
                MockDirectoryTreeViewAfterSelect(_sutPresenter, node => treeViewSelectedNode = node);
                TreeNode treeViewRootNode = null;
                _mockForm.DirectoryTreeViewNodes = Arg.Do<TreeNode>(x => treeViewRootNode = x);
                List<ICommonEntry> listViewListArg = null;
                _mockDirectoryListViewHelper.SetList(Arg.Do<List<ICommonEntry>>(x => listViewListArg = x));
                FakeItemActivateWithValue(_mockSearchResultListViewHelper, _pairDirEntry);

                // ACT
                _sutPresenter.SearchResultListViewItemActivate(); // activating from search result

                _mockForm.Received(1).DirectoryTreeViewNodes = treeViewRootNode;
                Assert.That(treeViewSelectedNode.Text, Is.EqualTo("Test1"),
                    "TreeView root node Text value not expected");
                _mockDirectoryListViewHelper.Received(1).SetList(Arg.Any<List<ICommonEntry>>());
                Assert.That(listViewListArg.Count, Is.EqualTo(0), "Nothing displayed in list view for empty folder");
            }
        }

        [TestFixture]
        public class CatalogListViewItemActivate : TestCDEWinPresenterBase
        {
            private CDEWinFormPresenter _sutPresenter;
            readonly IConfiguration _config = Substitute.For<IConfiguration>();

            [SetUp]
            public override void RunBeforeEveryTest()
            {
                _config.ProgressUpdateInterval.Returns(5000);
                base.RunBeforeEveryTest();
                InitRootWithDir();
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig);
                InitRootWithFile();
            }

            [Test]
            public void Catalog_Activate_On_Same_RootEntry_Does_Not_Set_Root()
            {
                var testRootTreeNode = new TreeNode("Moo") { Tag = _rootEntry };
                _mockForm.DirectoryTreeViewNodes.Returns(testRootTreeNode);
                FakeItemActivateWithValue(_mockCatalogListViewHelper, _rootEntry);

                // ACT
                _sutPresenter.CatalogListViewItemActivate();

                _mockForm.DidNotReceiveWithAnyArgs().DirectoryTreeViewNodes = Arg.Any<TreeNode>();
            }

            [Test]
            public void Catalog_Activate_On_Different_RootEntry_Sets_New_Root()
            {
                var alternateRootEntry = new RootEntry(_config) { Path = "alternate" };
                var testRootTreeNode = new TreeNode("Moo") { Tag = alternateRootEntry };
                _mockForm.DirectoryTreeViewNodes.Returns(testRootTreeNode);
                TreeNode treeNodeSet = null;
                _mockForm.DirectoryTreeViewNodes =
                    Arg.Do<TreeNode>(node => { treeNodeSet = node; });
                FakeItemActivateWithValue(_mockCatalogListViewHelper, _rootEntry);

                // ACT
                _sutPresenter.CatalogListViewItemActivate();

                // _mockForm.Received(1).DirectoryTreeViewNodes = Arg.Any<TreeNode>();
                Assert.That(treeNodeSet.Tag, Is.EqualTo(_rootEntry));
            }

            [Test]
            public void Catalog_Activate_GoToDirectoryRoot_On_Null_RootNode_Sets_New_Root()
            {
                _mockForm.DirectoryTreeViewNodes.Returns((TreeNode)null);
                FakeItemActivateWithValue(_mockCatalogListViewHelper, _rootEntry);

                // ACT                
                _sutPresenter.CatalogListViewItemActivate();

                _mockForm.Received(1).DirectoryTreeViewNodes = Arg.Any<TreeNode>();
            }

            [Test]
            public void Callback_GoToDirectoryRoot_Sets_Directory_Pane()
            {
                FakeItemActivateWithValue(_mockCatalogListViewHelper, _rootEntry);

                _sutPresenter.CatalogListViewItemActivate();

                _mockForm.Received(1).SelectDirectoryPane();
            }

            [Test]
            public void Callback_GoToDirectoryRoot_Setting_RootNode_Calls_InitSort()
            {
                FakeItemActivateWithValue(_mockCatalogListViewHelper, _rootEntry);

                _sutPresenter.CatalogListViewItemActivate();

                _mockDirectoryListViewHelper.Received(1).InitSort();
            }

            [Ignore("Invalid test as calling action with null isn't allowed in app")]
            [Test]
            public void ItemActive_Setting_Value_Null_Throws_Exception()
            {
                FakeItemActivateWithValue(_mockCatalogListViewHelper, null);

                _sutPresenter.CatalogListViewItemActivate();
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
                _stubConfig.DefaultCatalogColumnCount.Returns(13); // enough spaces for catalog list view items.
                _stubConfig.DateFormatYMDHMS.Returns("{0:yyyy/MM/dd HH:mm:ss}");
                InitRootWithFile();
                var _loadCatalogsService = Substitute.For<ILoadCatalogService>();
                _loadCatalogsService.LoadRootEntries(Arg.Any<IConfig>(), Arg.Any<TimeIt>())
                    .Returns(_rootList);
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, _loadCatalogsService);
            }

            [Test]
            public void Produces_ListView_Field()
            {
                _stubConfig.DateFormatYMDHMS.Returns("{0:yyyy/MM}"); // make local time zone irrelevant for test.
                _mockCatalogListViewHelper.RetrieveItemIndex.Returns(0);
                ListViewItem setRenderItem = null;
                _mockCatalogListViewHelper.RenderItem = Arg.Do<ListViewItem>(lvi => { setRenderItem = lvi; });

                // ACT                
                _sutPresenter.CatalogRetrieveVirtualItem();

                var expectedValues = new[]
                {
                    @"T:\", string.Empty/*"TestVolume"*/, "0", "1", "1",
                    "Z", "531 B", "736.6 KB", "639 KB",
                    "2011/12", // "/02 03:15:13",
                    "34 msec",
                    @".\TestRootEntry.cde",
                    "Test Root Entry Description"
                };
                setRenderItem.AssertListViewSubItemEqualValues(expectedValues);
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
                _stubConfig.DefaultSearchResultColumnCount
                    .Returns(5); // enough spaces for search result list view items.
                _stubConfig.DateFormatYMDHMS.Returns("{0:yyyy/MM/dd HH:mm:ss}");

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
            /// This is not something that should happen as list view wont ask for
            /// an Item Index that is outside bounds of the setup ListView.
            /// </summary>
            [Test]
            public void Invalid_ItemIndex_With_List_Wrong_Index_Throws_Exception()
            {
                var pairDirList = new List<PairDirEntry> { _pairDirEntry };
                _sutPresenter.TestSetSearchResultList(pairDirList);
                _mockSearchResultListViewHelper.RetrieveItemIndex.Returns(1);

                Assert.Throws<ArgumentOutOfRangeException>(() => _sutPresenter.SearchResultRetrieveVirtualItem());
            }

            [Test]
            public void Valid_DirEntry()
            {
                var pairDirList = new List<PairDirEntry> { _pairDirEntry };
                _sutPresenter.TestSetSearchResultList(pairDirList);
                _mockSearchResultListViewHelper.RetrieveItemIndex.Returns(0);

                ListViewItem setRenderItem = null;
                _mockSearchResultListViewHelper.RenderItem = Arg.Do<ListViewItem>(lvi => { setRenderItem = lvi; });

                // ACT
                _sutPresenter.SearchResultRetrieveVirtualItem();

                var expectedValues = new[]
                {   // Fragile, this depends on time zone of test machine at moment.
                    "Test", "531", "2010/11/02 18:16:12", "TestRootEntry.cde", @"T:\"
                };
                setRenderItem.AssertListViewSubItemEqualValues(expectedValues);
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
                var testRootTreeNode = new TreeNode("MyTestNode") { Tag = _rootEntry };
                _mockForm.DirectoryTreeViewActiveAfterSelectNode = testRootTreeNode;
                List<ICommonEntry> list = null;
                _mockDirectoryListViewHelper.SetList(Arg.Do<List<ICommonEntry>>(x => list = x));
                var pathTextBoxValue = string.Empty;
                _mockForm.SetDirectoryPathTextbox = Arg.Do<string>(x => pathTextBoxValue = x);

                // ACT
                _sutPresenter.DirectoryTreeViewAfterSelect();

                Assert.That(list, Is.Not.Null);
                Assert.That(list.Count, Is.EqualTo(1), "The list set on ListViewHelper wasn't expected");
                Assert.That(list[0].Path, Is.EqualTo("Test"), "The list set on ListViewHelper wasn't expected");
                Assert.That(list[0].Size, Is.EqualTo(531), "The list set on ListViewHelper wasn't expected");
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

                _stubConfig.DefaultDirectoryColumnCount.Returns(3); // enough spaces for directory list view items.
                InitRootWithFile();
                _sutPresenter = new CDEWinFormPresenter(_mockForm, _stubConfig, null);
            }

            [Test]
            public void Nothing_Bombs_If_No_ListViewEntries()
            {
                _sutPresenter.DirectoryRetrieveVirtualItem();
            }

            [Test]
            public void Nothing_Bombs_With_List_Empty_Does_Nothing()
            {
                MockDirectoryTreeViewAfterSelect(_sutPresenter);

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
                    Assert.Fail($"At index {i} expected item \"{expect}\" did not match \"{got}\"");
                }
                else
                {
                    // ReSharper disable once LocalizableElement
                    Console.WriteLine($"{i} {expect}");
                }
            }
        }
    }

    public class TestPresenterSetSearch : CDEWinFormPresenter
    {
        public TestPresenterSetSearch(ICDEWinForm form, IConfig config) : base(form, config, null)
        {
        }

        public int TestSetSearchResultList(List<PairDirEntry> list)
        {
            return SetSearchResultList(list);
        }
    }
}

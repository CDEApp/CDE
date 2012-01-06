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
        protected IListViewHelper<PairDirEntry> _stubSearchResultListViewHelper;
        protected IListViewHelper<DirEntry> _mockDirectoryListViewHelper;
        protected IListViewHelper<RootEntry> _stubCatalogListViewHelper;

        protected RootEntry _rootEntry;
        protected DirEntry _dirEntry;
        protected PairDirEntry _pairDirEntry;

        [SetUp]
        public virtual void RunBeforeEveryTest()
        {
            _mockForm = MockRepository.GenerateMock<ICDEWinForm>();
            _stubConfig = MockRepository.GenerateStub<IConfig>();

            _stubSearchResultListViewHelper = MockRepository.GenerateStub<IListViewHelper<PairDirEntry>>();
            _mockForm.Stub(x => x.SearchResultListViewHelper)
                .Return(_stubSearchResultListViewHelper);

            _mockDirectoryListViewHelper = MockRepository.GenerateMock<IListViewHelper<DirEntry>>();
            _mockForm.Stub(x => x.DirectoryListViewHelper)
                .Return(_mockDirectoryListViewHelper);

            _stubCatalogListViewHelper = MockRepository.GenerateStub<IListViewHelper<RootEntry>>();
            _mockForm.Stub(x => x.CatalogListViewHelper)
                .Return(_stubCatalogListViewHelper);
        }

        protected void InitRoot()
        {
            _rootEntry = new RootEntry {Path = @"T:\"};
            _dirEntry = new DirEntry {Path = @"Test"};
            _rootEntry.Children.Add(_dirEntry);
            _rootEntry.SetInMemoryFields();
            _pairDirEntry = new PairDirEntry(_rootEntry, _dirEntry);
        }
    }
    // ReSharper restore InconsistentNaming
}
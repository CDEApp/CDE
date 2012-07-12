using System;
using System.Collections.Generic;
using System.Windows.Forms;
using cdeLib;

namespace cdeWin
{
    public interface ICDEWinForm : IView
    {
        event EventAction OnDirectoryTreeViewBeforeExpandNode;
        event EventAction OnDirectoryTreeViewAfterSelect;
        event EventAction OnMyFormClosing;
        event EventAction OnExitMenuItem;
        event EventAction OnSearch;
        event EventAction OnAboutMenuItem;
        
        event EventAction OnSearchResultContextMenuViewTreeClick;
        event EventAction OnSearchResultContextMenuOpenClick;
        event EventAction OnSearchResultContextMenuExploreClick;
        event EventAction OnSearchResultContextMenuPropertiesClick;
        event EventAction OnSearchResultContextMenuSelectAllClick;

        event EventAction OnDirectoryContextMenuViewTreeClick;
        event EventAction OnDirectoryContextMenuOpenClick;
        event EventAction OnDirectoryContextMenuExploreClick;
        event EventAction OnDirectoryContextMenuPropertiesClick;
        event EventAction OnDirectoryContextMenuSelectAllClick;
        event EventAction OnDirectoryContextMenuParentClick;

        event EventAction OnDirectoryRetrieveVirtualItem;
        void OnDirectoryRetrieveVirtualItemFire();
        event EventAction OnDirectoryListViewItemActivate;
        void OnDirectoryListViewItemActivateFire();
        event EventAction OnDirectoryListViewColumnClick;
        void OnDirectoryListViewColumnClickFire();
        event EventAction OnDirectoryListViewItemSelectionChanged;
        void OnDirectoryListViewItemSelectionChangedFire();

        event EventAction OnSearchResultRetrieveVirtualItem;
        void OnSearchResultRetrieveVirtualItemFire();
        event EventAction OnSearchResultListViewItemActivate;
        void OnSearchResultListViewItemActivateFire();
        event EventAction OnSearchResultListViewColumnClick;
        void OnSearchResultListViewColumnClickFire();

        event EventAction OnCatalogRetrieveVirtualItem;
        void OnCatalogRetrieveVirtualItemFire();
        event EventAction OnCatalogListViewItemActivate;
        void OnCatalogListViewItemActivateFire();
        event EventAction OnCatalogListViewColumnClick;
        void OnCatalogListViewColumnClickFire();

        event EventAction OnAdvancedSearchCheckboxChanged;

        event EventAction OnDirectoryTreeContextMenuOpenClick;
        event EventAction OnDirectoryTreeContextMenuExploreClick;
        event EventAction OnDirectoryTreeContextMenuPropertiesClick;

        TreeNode DirectoryTreeViewNodes { get;  set; }

        TreeNode DirectoryTreeViewActiveBeforeExpandNode { get; set; }
        TreeNode DirectoryTreeViewActiveAfterSelectNode { get; set; }

        string Pattern { get; set; }
        bool RegexMode { get; set; }
        bool IncludeFiles { get; }
        bool IncludeFolders { get; }
        int FindEntryFilter { get; set; }
        bool IncludePathInSearch { get; set; }

        void SetSearchTextBoxAutoComplete(IEnumerable<string> history);
        void AddSearchTextBoxAutoComplete(string pattern);
        List<string> GetSearchTextBoxAutoComplete();
        void SelectDirectoryPane();
        float DirectoryPanelSplitterRatio { get; set; }
        string SetDirectoryPathTextbox { get; set; }
        TreeNode DirectoryTreeViewSelectedNode { get; set; }
        void SetSearchResultStatus(int i);
        void SetTotalFileEntriesLoadedStatus(int i);
        void SetCatalogsLoadedStatus(int i);
        void SetSearchTimeStatus(string s);
        bool SearchButtonEnable { get; set; }
        string SearchButtonText { get; set; }
        bool IsAdvancedSearchMode { get; set; }
        DateTime FromDateValue { get; set; }
        DateTime ToDateValue { get; set; }
        DateTime FromHourValue { get; set; }
        DateTime ToHourValue { get; set; }

        IListViewHelper<PairDirEntry> SearchResultListViewHelper { get; set; }
        IListViewHelper<DirEntry> DirectoryListViewHelper { get; set; }
        IListViewHelper<RootEntry> CatalogListViewHelper { get; set; }

        CheckBoxDependentControlHelper FromDate { get; set; }
        CheckBoxDependentControlHelper ToDate { get; set; }
        CheckBoxDependentControlHelper FromHour { get; set; }
        CheckBoxDependentControlHelper ToHour { get; set; }
        CheckBoxDependentControlHelper FromSize { get; set; }
        CheckBoxDependentControlHelper ToSize { get; set; }
        CheckBoxDependentControlHelper NotOlderThan { get; set; }

        DropDownHelper<int> LimitResultHelper { get; set; }
        DropDownHelper<int> FromSizeDropDownHelper { get; set; }
        DropDownHelper<int> ToSizeDropDownHelper { get; set; }
        DropDownHelper<AddTimeUnitFunc> NotOlderThanDropDownHelper { get; set; }

        UpDownHelper FromSizeValue { get; set; }
        UpDownHelper ToSizeValue { get; set; }
        UpDownHelper NotOlderThanValue { get; set; }

        void CleanUp();

        // improve test easy on CDEWinFormPresenter.
        void SetColumnSortCompare<T>(IListViewHelper<T> lvh, Comparison<T> compare) where T : class;
        // improve test easy on CDEWinFormPresenter.
        int SetList<T>(IListViewHelper<T> lvh, List<T> list) where T : class;
        // improve test easy on CDEWinFormPresenter.
        void SortList<T>(IListViewHelper<T> lvh) where T : class;

        void MessageBox(string message);
		void AboutDialog();
		void Addline(string format, params object[] args);
        CommonEntry GetSelectedTreeItem();
    }
}
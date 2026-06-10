using System;
using System.Collections.Generic;
using System.Windows.Forms;
using cdeWin.Cfg;

namespace cdeWin;

public interface IListViewHelper<T> : IDisposable where T : class
{
    /// <summary>
    /// Used by virtual mode ListView
    /// </summary>
    int RetrieveItemIndex { get; set; }

    ListViewItem RenderItem { get; set; }

    IEnumerable<int> SelectedIndices { get; set; }

    int SelectedIndicesCount { get; set; }

    SortOrder ColumnSortOrder { get; set; }

    int SortColumn { get; set; }

    Comparison<T> ColumnSortCompare { get; set; }

    void InitSort();
    IEnumerable<ColumnConfig> ColumnConfigs();
    void SetColumnConfigs(IEnumerable<ColumnConfig> columns);
    void ForceDraw();
    void SelectItem(int index);
    void DeselectAllItems();
    void SelectAllItems();
    int SetList(List<T> list);
    void ListViewColumnClick();
    void SortList();
    void ActionOnSelectedItems(Action<IEnumerable<T>> action);
    void ActionOnSelectedItem(Action<T> action);
    void ActionOnActivateItem(Action<T> action);
    T GetItemAt(int index);
    void SearchListContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e);
}
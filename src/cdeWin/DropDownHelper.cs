using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace cdeWin
{
    public class DropDownHelper<T>
    {
        private readonly ComboBox _comboBox;

        public DropDownHelper(ComboBox comboBox, IEnumerable<ComboBoxItem<T>> items, int selectedIndex)
        {
            _comboBox = comboBox;
            _comboBox.Items.AddRange(items.ToArray());
            _comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            SelectedIndex = selectedIndex;
        }

        public int SelectedIndex
        {
            get => _comboBox.SelectedIndex;
            set
            {
                if (value >= 0)
                {
                    _comboBox.SelectedIndex = value;
                }
            }
        }

        public T SelectedValue => ((ComboBoxItem<T>)_comboBox.SelectedItem).Value;
    }
}
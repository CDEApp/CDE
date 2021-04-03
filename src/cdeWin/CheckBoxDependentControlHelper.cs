using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace cdeWin
{
    /// <summary>
    /// Manage a set of control which are enabled/disabled relative to primaryCheckbox.
    /// </summary>
    public class CheckBoxDependentControlHelper // <T>
    {
        private readonly CheckBox _primaryCheckBox;
        private readonly IEnumerable<Control> _dependentControls;
        private readonly IEnumerable<CheckBox> _mutuallyExclusiveCheckBoxes;

        // ReSharper disable PossibleMultipleEnumeration
        public CheckBoxDependentControlHelper(CheckBox primaryCheckbox, IEnumerable<Control> dependentControls, IEnumerable<CheckBox> mutuallyExclusiveCheckBoxes)
        {
            if (mutuallyExclusiveCheckBoxes?.FirstOrDefault(x => x == primaryCheckbox) != null)
            {
                throw new ArgumentException("Primary checkbox cannot appear in other parameter sequences.", nameof(mutuallyExclusiveCheckBoxes));
            }

            _primaryCheckBox = primaryCheckbox;
            _dependentControls = dependentControls;
            _mutuallyExclusiveCheckBoxes = mutuallyExclusiveCheckBoxes;
            _primaryCheckBox.CheckedChanged += CheckboxChanged;
            SetDependentControlState(_primaryCheckBox.Checked);
        }
        // ReSharper restore PossibleMultipleEnumeration

        private void CheckboxChanged(object sender, EventArgs e)
        {
            SetDependentControlState(_primaryCheckBox.Checked);
        }

        private void SetDependentControlState(bool boxChecked)
        {
            if (boxChecked && _mutuallyExclusiveCheckBoxes != null)
            {
                foreach (var mutuallyExclusiveControl in _mutuallyExclusiveCheckBoxes)
                {
                    mutuallyExclusiveControl.Checked = false;
                }
            }
            if (_dependentControls != null)
            {
                foreach (var dependentControl in _dependentControls)
                {
                    dependentControl.Enabled = boxChecked;
                }
            }
            _primaryCheckBox.Checked = boxChecked;
        }

        public bool Checked
        {
            get { return _primaryCheckBox.Checked; }
            set { _primaryCheckBox.Checked = value; }
        }
    }
}
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
        private readonly IEnumerable<Control> _depedentControls;
        private readonly IEnumerable<CheckBox> _mutuallyExclusiveCheckBoxs;

        // ReSharper disable PossibleMultipleEnumeration
        public CheckBoxDependentControlHelper(CheckBox primaryCheckbox, IEnumerable<Control> depedentControls, IEnumerable<CheckBox> mutuallyExclusiveCheckBoxs)
        {
            if (mutuallyExclusiveCheckBoxs != null)
            {
                if (mutuallyExclusiveCheckBoxs.FirstOrDefault(x => x == primaryCheckbox) != null)
                {
                    throw new ArgumentException("Primary checkbox cannot appear in other parameter sequences.", "mutuallyExclusiveCheckBoxs");
                }
            }

            _primaryCheckBox = primaryCheckbox;
            _depedentControls = depedentControls;
            _mutuallyExclusiveCheckBoxs = mutuallyExclusiveCheckBoxs;
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
            if (boxChecked && _mutuallyExclusiveCheckBoxs != null)
            {
                foreach (var mutuallyExclusiveControl in _mutuallyExclusiveCheckBoxs)
                {
                    mutuallyExclusiveControl.Checked = false;
                }
            }
            if (_depedentControls != null)
            {
                foreach (var dependentControl in _depedentControls)
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
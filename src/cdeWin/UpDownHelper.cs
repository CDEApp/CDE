using System.Windows.Forms;

namespace cdeWin
{
    public class UpDownHelper
    {
        private readonly NumericUpDown _upDown;

        // have a field - text box / up-down 
        // have a drop down which modifies the the field... like a multiplier or offset.
        public UpDownHelper(NumericUpDown upDown, int decimalPlaces = 2)
        {
            _upDown = upDown;
            _upDown.DecimalPlaces = decimalPlaces;
            _upDown.Minimum = 0;
            _upDown.Maximum = int.MaxValue;
        }

        public decimal Field
        {
            get => _upDown.Value;
            set => _upDown.Value = value;
        }
    }
}
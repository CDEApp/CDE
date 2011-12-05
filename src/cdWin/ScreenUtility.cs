using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace cdeWin
{
    public static class ScreenUtility
    {
        public static bool IsVisibleOnAnyScreen(this Rectangle rect)
        {
            return Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(rect));
        }
    }
}
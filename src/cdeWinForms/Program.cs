using System;
using System.Windows.Forms;
using cdeWinFormsPresenter;

namespace cdeWinForms
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new CDEMainForm();
            form.Attach(new CDEMainPresenter(form));
            Application.Run(form);
        }
    }
}

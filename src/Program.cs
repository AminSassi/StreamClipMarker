using System;
using System.Windows.Forms;

namespace StreamClipMarker
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                // Last-ditch crash safety
                try
                {
                    Exception ex = e.ExceptionObject as Exception;
                    string msg = ex != null ? ex.Message : "Unknown fatal error";
                    MessageBox.Show("StreamClipMarker encountered an error: " + msg,
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            };

            Application.Run(new MainForm());
        }
    }
}

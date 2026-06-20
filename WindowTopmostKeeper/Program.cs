using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowTopmostKeeper
{
    static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "Global\\WindowTopmostKeeper_UniqueMutexName", out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("既に起動しています。", "二重起動防止", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            finally
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
        }
    }
}
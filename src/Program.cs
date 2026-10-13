using System;
using System.Threading;
using System.Windows.Forms;

namespace MiniTrayMon
{
    internal static class Program
    {
        private const string MutexName = "Global\\MiniTrayMon_SingleInstance_v1";

        [STAThread]
        private static void Main()
        {
            // Single-instance guard
            bool createdNew;
            Mutex mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                // Already running — silently exit
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new TrayApp());
            }
            finally
            {
                // Release the mutex; keep a reference until the app exits
                GC.KeepAlive(mutex);
            }
        }
    }
}

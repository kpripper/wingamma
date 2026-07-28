using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WinGamma
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 2
                    && String.Equals(args[0], "--install-profile",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ProfileService.InstallElevated(args[1]) ? 0 : 2;
                }

                if (args.Length >= 1
                    && String.Equals(args[0], "--self-test",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return SelfTests.Run();
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender,
                    ThreadExceptionEventArgs exception)
                {
                    SettingsStore.Log("UI exception: " + exception.Exception);
                    MessageBox.Show(exception.Exception.Message, "WinGamma",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                if (args.Length >= 1
                    && String.Equals(args[0], "--loader",
                        StringComparison.OrdinalIgnoreCase))
                    return RunLoader();

                return RunEditor();
            }
            catch (Exception exception)
            {
                SettingsStore.Log("Fatal error: " + exception);
                try
                {
                    MessageBox.Show(exception.Message, "WinGamma",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                }
                return 1;
            }
        }

        private static int RunLoader()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\WinGamma.Loader",
                out created))
            {
                if (!created)
                    return 0;
                Application.Run(new LoaderContext());
                return 0;
            }
        }

        private static int RunEditor()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\WinGamma.EditorActive",
                out created))
            {
                if (!created)
                {
                    MessageBox.Show("WinGamma editor is already running.",
                        "WinGamma", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return 0;
                }
                Application.Run(new MainForm());
                return 0;
            }
        }
    }
}

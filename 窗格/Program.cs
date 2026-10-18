using 窗格.Model;
using System;
using System.Threading;
using System.Windows.Forms;
using 窗格.Win32;

namespace 窗格
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //allows the context menu to be in dark mode
            //inherits from the system settings
            WindowUtil.SetPreferredAppMode(1);

            using (var mutex = new Mutex(true, "No_fences", out var createdNew))
            {
                if (createdNew)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    using (var trayIcon = new TrayIconController())
                    {
                        var loadResult = FenceManager.Instance.LoadFences();
                        if (Application.OpenForms.Count == 0)
                            FenceManager.Instance.CreateFence("First fence");

                        if (loadResult.LoadedFromJson > 0 || loadResult.LoadedFromBackup > 0 || loadResult.SkippedPaths.Count > 0)
                        {
                            var messages = new System.Collections.Generic.List<string>();
                            if (loadResult.LoadedFromJson > 0 || loadResult.LoadedFromBackup > 0)
                            {
                                messages.Add("Loaded " + loadResult.LoadedFromJson + " fence(s) from JSON storage and "
                                    + loadResult.LoadedFromBackup + " fence(s) from XML backups. Original source files were left untouched.");
                            }

                            if (loadResult.SkippedPaths.Count > 0)
                            {
                                messages.Add("Some fence data could not be loaded from these paths:"
                                    + Environment.NewLine + string.Join(Environment.NewLine, loadResult.SkippedPaths));
                            }

                            var message = string.Join(Environment.NewLine + Environment.NewLine, messages);
                            MessageBox.Show(message, "窗格", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        Application.Run();
                    }
                }
            }
        }

    }
}

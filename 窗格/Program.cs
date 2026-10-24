using 窗格.Model;
using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using 窗格.Properties;
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
                        FenceWindowCoordinator.ShowFences(loadResult.Fences);
                        if (loadResult.Fences.Count == 0)
                            FenceWindowCoordinator.CreateFence(UiText.Get("First fence"));

                        var skippedPaths = loadResult.SkippedPaths
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        string settingsError;
                        if (ShouldShowSkippedPathsWarning(skippedPaths, out settingsError))
                        {
                            var message = UiText.Get("Some fence data could not be loaded from these paths:")
                                + Environment.NewLine + string.Join(Environment.NewLine, skippedPaths)
                                + Environment.NewLine + Environment.NewLine
                                + UiText.Get("To inspect the data, open Advanced Settings and use the fence data folder. This warning will not repeat unless the skipped paths change or a clean load occurs first.");
                            if (settingsError != null)
                            {
                                message += Environment.NewLine + Environment.NewLine
                                    + UiText.Get("The warning preference could not be saved, so this message may appear again.")
                                    + Environment.NewLine + settingsError;
                            }

                            MessageBox.Show(message, "窗格", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        Application.Run();
                    }
                }
            }
        }

        private static bool ShouldShowSkippedPathsWarning(System.Collections.Generic.IList<string> skippedPaths, out string settingsError)
        {
            settingsError = null;
            var signature = string.Join(Environment.NewLine, skippedPaths);

            try
            {
                var settings = Settings.Default;
                if (string.Equals(settings.LastSkippedFencePaths, signature, StringComparison.OrdinalIgnoreCase))
                    return false;

                settings.LastSkippedFencePaths = signature;
                settings.Save();
                return skippedPaths.Count > 0;
            }
            catch (ConfigurationErrorsException ex)
            {
                settingsError = ex.Message;
                return skippedPaths.Count > 0;
            }
        }

    }
}

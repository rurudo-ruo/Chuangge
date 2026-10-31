using 窗格.Model;
using 窗格.Util;
using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using 窗格.Properties;
using 窗格.Win32;

namespace 窗格
{
    static class Program
    {
        private static bool handlingFatalError;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            AppLogger.Info("窗格 " + Application.ProductVersion + " starting on "
                + Environment.OSVersion.VersionString);

            try
            {
                Run();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Startup failed", ex);
                ShowFatalError();
            }
        }

        private static void Run()
        {
            // Allows the context menu to be in dark mode; inherits from the system settings.
            try
            {
                WindowUtil.SetPreferredAppMode(1);
            }
            catch (EntryPointNotFoundException)
            {
                AppLogger.Info("The dark context-menu preference is not available on this Windows build.");
            }

            using (var mutex = new Mutex(true, "No_fences", out var createdNew))
            {
                if (!createdNew)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var trayIcon = new TrayIconController())
                {
                    var loadResult = FenceManager.Instance.LoadFences();
                    AppLogger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Loaded {0} fence(s): {1} from JSON, {2} from backup, {3} skipped.",
                        loadResult.Fences.Count, loadResult.LoadedFromJson,
                        loadResult.LoadedFromBackup, loadResult.SkippedPaths.Count));
                    foreach (var skippedPath in loadResult.SkippedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
                        AppLogger.Info("Skipped fence data: " + skippedPath);

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

                    AppLogger.Info("Startup complete.");
                    Application.Run();
                    AppLogger.Info("窗格 exited.");
                }
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            AppLogger.Error("Unhandled UI exception", e.Exception);

            if (handlingFatalError)
            {
                Environment.Exit(1);
                return;
            }

            handlingFatalError = true;
            ShowFatalError();
            Application.Exit();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLogger.Error("Unhandled background exception", e.ExceptionObject as Exception);
        }

        private static void ShowFatalError()
        {
            try
            {
                var message = UiText.Get("窗格 encountered an unexpected error and will close so its data can be saved. Details were written to this folder:")
                    + Environment.NewLine + AppLogger.LogDirectory;
                MessageBox.Show(message, UiText.Get("窗格 encountered an error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // The dialog is best effort; the log entry is what matters.
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

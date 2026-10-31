using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using 窗格.Win32;

namespace 窗格.Util
{
    internal static class StartupRegistration
    {
        private const string ShortcutName = "NoFences-Startup.lnk";

        public static bool IsEnabled
        {
            get { return File.Exists(GetShortcutPath()); }
        }

        public static void SetEnabled(bool enabled)
        {
            var shortcutPath = GetShortcutPath();
            if (!enabled)
            {
                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);
                return;
            }

            object link = null;
            try
            {
                link = new ShellLink();
                var shellLink = (IShellLinkW)link;
                shellLink.SetPath(Application.ExecutablePath);
                shellLink.SetWorkingDirectory(Application.StartupPath);
                shellLink.SetDescription(UiText.Get("Start NoFences when I sign in"));
                ((IPersistFile)link).Save(shortcutPath, true);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException(UiText.Get("Windows could not update the startup shortcut."), ex);
            }
            finally
            {
                if (link != null && Marshal.IsComObject(link))
                    Marshal.FinalReleaseComObject(link);
            }
        }

        private static string GetShortcutPath()
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (string.IsNullOrWhiteSpace(startupFolder))
                throw new InvalidOperationException(UiText.Get("The current user's Windows Startup folder could not be located."));

            return Path.Combine(startupFolder, ShortcutName);
        }
    }
}

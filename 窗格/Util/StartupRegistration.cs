using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("Windows Script Host is unavailable, so the startup shortcut could not be created.");

            object shell = null;
            object shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { shortcutPath });
                var shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty,
                    null, shortcut, new object[] { Application.ExecutablePath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty,
                    null, shortcut, new object[] { Application.StartupPath });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty,
                    null, shortcut, new object[] { "Start NoFences when you sign in" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException("Windows could not update the startup shortcut.",
                    ex.InnerException ?? ex);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                    Marshal.FinalReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.FinalReleaseComObject(shell);
            }
        }

        private static string GetShortcutPath()
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (string.IsNullOrWhiteSpace(startupFolder))
                throw new InvalidOperationException("The current user's Windows Startup folder could not be located.");

            return Path.Combine(startupFolder, ShortcutName);
        }
    }
}

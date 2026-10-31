using System.Diagnostics;

namespace 窗格.Util
{
    internal static class ShellLaunch
    {
        /// <summary>
        /// Opens a file or folder with the shell. The path is passed as the file name rather than
        /// as a command line, so no quoting or argument parsing is involved.
        /// </summary>
        public static void Open(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}

using System;
using 窗格.Properties;

namespace 窗格
{
    internal static class GlobalFenceSettings
    {
        public static event EventHandler Changed;

        public static int TitleHeight => Math.Max(16, Math.Min(100, Settings.Default.GlobalTitleHeight));

        public static bool HideTitleArea => Settings.Default.HideTitleArea;

        public static void Update(int titleHeight, bool hideTitleArea)
        {
            Settings.Default.GlobalTitleHeight = Math.Max(16, Math.Min(100, titleHeight));
            Settings.Default.HideTitleArea = hideTitleArea;
            Settings.Default.Save();
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}

using System;
using System.Drawing;
using Microsoft.Win32;
using 窗格.Properties;

namespace 窗格
{
    internal static class GlobalFenceSettings
    {
        public static event EventHandler Changed;

        static GlobalFenceSettings()
        {
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            ApplyTheme();
        }

        public static int TitleHeight => Math.Max(16, Math.Min(100, Settings.Default.GlobalTitleHeight));

        public static bool HideTitleArea => Settings.Default.HideTitleArea;

        public static bool UseSystemTheme => Settings.Default.UseSystemTheme;

        public static string DisplayMode => IsDisplayModeDark(Settings.Default.DisplayMode) ? "Dark" : "Light";

        public static Color CustomAccentColor => Color.FromArgb(Settings.Default.CustomAccentColor);

        public static bool IsDarkTheme => FluentTheme.IsDark;

        public static Color AccentColor => FluentTheme.Accent;

        public static int GetOverlayOpacity(int darkDefault, int lightDefault)
        {
            var value = FluentTheme.IsDark
                ? Settings.Default.DarkOverlayOpacity
                : Settings.Default.LightOverlayOpacity;
            var baseline = FluentTheme.IsDark ? darkDefault : lightDefault;
            return Math.Max(0, Math.Min(255, (int)Math.Round(value * (double)baseline / (FluentTheme.IsDark ? 92 : 174))));
        }

        public static int DarkOverlayOpacity => Math.Max(0, Math.Min(255, Settings.Default.DarkOverlayOpacity));

        public static int LightOverlayOpacity => Math.Max(0, Math.Min(255, Settings.Default.LightOverlayOpacity));

        public static bool GlowEffectsEnabled => Settings.Default.GlowEffectsEnabled;

        public static Color PointerGlowColor => Color.FromArgb(Settings.Default.PointerGlowColor);

        public static int PointerGlowIntensity => Math.Max(0, Math.Min(100, Settings.Default.PointerGlowIntensity));

        public static int PointerGlowRadius => Math.Max(32, Math.Min(128, Settings.Default.PointerGlowRadius));

        public static int IconGlowIntensity => Math.Max(0, Math.Min(100, Settings.Default.IconGlowIntensity));

        public static int IconGlowRadius => Math.Max(2, Math.Min(18, Settings.Default.IconGlowRadius));

        public static bool IsDisplayModeDark(string displayMode)
        {
            return string.Equals(displayMode, "Dark", StringComparison.OrdinalIgnoreCase);
        }

        public static void Update(int titleHeight, bool hideTitleArea, bool useSystemTheme,
            string displayMode, int customAccentColor, int darkOverlayOpacity, int lightOverlayOpacity,
            bool glowEffectsEnabled, int pointerGlowColor, int pointerGlowIntensity, int pointerGlowRadius,
            int iconGlowIntensity, int iconGlowRadius)
        {
            Settings.Default.GlobalTitleHeight = Math.Max(16, Math.Min(100, titleHeight));
            Settings.Default.HideTitleArea = hideTitleArea;
            Settings.Default.UseSystemTheme = useSystemTheme;
            Settings.Default.DisplayMode = IsDisplayModeDark(displayMode) ? "Dark" : "Light";
            Settings.Default.CustomAccentColor = customAccentColor;
            Settings.Default.DarkOverlayOpacity = Math.Max(0, Math.Min(255, darkOverlayOpacity));
            Settings.Default.LightOverlayOpacity = Math.Max(0, Math.Min(255, lightOverlayOpacity));
            Settings.Default.GlowEffectsEnabled = glowEffectsEnabled;
            Settings.Default.PointerGlowColor = pointerGlowColor;
            Settings.Default.PointerGlowIntensity = Math.Max(0, Math.Min(100, pointerGlowIntensity));
            Settings.Default.PointerGlowRadius = Math.Max(32, Math.Min(128, pointerGlowRadius));
            Settings.Default.IconGlowIntensity = Math.Max(0, Math.Min(100, iconGlowIntensity));
            Settings.Default.IconGlowRadius = Math.Max(2, Math.Min(18, iconGlowRadius));
            Settings.Default.Save();
            ApplyTheme();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.Color && e.Category != UserPreferenceCategory.General)
                return;

            if (!UseSystemTheme)
                return;

            ApplyTheme();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void ApplyTheme()
        {
            var isDark = UseSystemTheme
                ? !ReadSystemAppsUseLightTheme()
                : IsDisplayModeDark(Settings.Default.DisplayMode);
            var accent = UseSystemTheme
                ? ReadSystemAccentColor()
                : Color.FromArgb(Settings.Default.CustomAccentColor);
            FluentTheme.Configure(isDark, accent);
        }

        private static bool ReadSystemAppsUseLightTheme()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                var value = key == null ? null : key.GetValue("AppsUseLightTheme");
                return value == null || Convert.ToInt32(value) != 0;
            }
        }

        private static Color ReadSystemAccentColor()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM", false))
            {
                var value = key == null ? null : key.GetValue("AccentColor");
                if (value == null)
                    value = key == null ? null : key.GetValue("ColorizationColor");
                if (value != null)
                {
                    var color = unchecked((uint)Convert.ToInt64(value));
                    if (key.GetValue("AccentColor") != null)
                        return Color.FromArgb(255, (int)(color & 0xff), (int)((color >> 8) & 0xff), (int)((color >> 16) & 0xff));

                    return Color.FromArgb(255, (int)((color >> 16) & 0xff), (int)((color >> 8) & 0xff), (int)(color & 0xff));
                }
            }

            using (var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", false))
            {
                var value = key == null ? null : key.GetValue("AccentColorMenu");
                if (value != null)
                {
                    var color = unchecked((uint)Convert.ToInt64(value));
                    return Color.FromArgb(255, (int)(color & 0xff), (int)((color >> 8) & 0xff), (int)((color >> 16) & 0xff));
                }
            }

            return SystemColors.Highlight;
        }
    }
}

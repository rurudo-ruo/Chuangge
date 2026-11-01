using System;
using System.Runtime.InteropServices;

namespace 窗格.Win32
{
    public class WindowUtil
    {
        public const int WM_NCHITTEST = 0x84;          // variables for dragging the form
        public const int HTCLIENT = 0x1;
        public const int HTCAPTION = 0x2;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;

        public const int WM_NCCALCSIZE = 0x0083;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int WM_SYSCOMMAND = 274;
        public const int SC_MAXIMIZE = 0xF030;
        public const int SC_MINIMIZE = 0xF020;

        public const UInt32 SWP_NOSIZE = 0x0001;
        public const UInt32 SWP_NOMOVE = 0x0002;
        public const UInt32 SWP_NOACTIVATE = 0x0010;
        public const UInt32 SWP_NOZORDER = 0x0004;
        public const int WM_ACTIVATEAPP = 0x001C;
        public const int WM_ACTIVATE = 0x0006;
        public const int WM_SETFOCUS = 0x0007;
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public const int WM_WINDOWPOSCHANGING = 0x0046;

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X,
           int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        public static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd,
           IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        public static extern IntPtr BeginDeferWindowPos(int nNumWindows);
        [DllImport("user32.dll")]
        public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Switches the native title bar between light and dark. Uses the Windows 10 2004+
        /// attribute and falls back to the 1809 attribute on older builds.
        /// </summary>
        public static void ApplyDarkTitleBar(IntPtr hWnd, bool dark)
        {
            const int useImmersiveDarkMode = 20;
            const int useImmersiveDarkModeBefore20H1 = 19;
            var value = dark ? 1 : 0;
            if (DropShadow.DwmSetWindowAttribute(hWnd, useImmersiveDarkMode, ref value, sizeof(int)) != 0)
                DropShadow.DwmSetWindowAttribute(hWnd, useImmersiveDarkModeBefore20H1, ref value, sizeof(int));
        }

        public static bool ApplyRoundedCorners(IntPtr hWnd, int width, int height, int radius)
        {
            const int dwmWindowCornerPreference = 33;
            const int dwmRound = 2;
            var cornerPreference = dwmRound;
            if (DropShadow.DwmSetWindowAttribute(hWnd, dwmWindowCornerPreference,
                ref cornerPreference, sizeof(int)) == 0)
                return true;

            var diameter = Math.Max(1, radius * 2);
            var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
            if (region == IntPtr.Zero)
                return false;

            if (SetWindowRgn(hWnd, region, true) != 0)
                return true;

            DeleteObject(region);
            return false;
        }

        /// <summary>
        /// Rounds the corners through DWM only. Unlike <see cref="ApplyRoundedCorners"/> this never
        /// installs a clipping region, so the system drop shadow survives on older Windows.
        /// </summary>
        public static void ApplyDwmRoundedCorners(IntPtr hWnd)
        {
            const int dwmWindowCornerPreference = 33;
            const int dwmRound = 2;
            var preference = dwmRound;
            DropShadow.DwmSetWindowAttribute(hWnd, dwmWindowCornerPreference, ref preference, sizeof(int));
        }

        /// <summary>
        /// Draws the window's hairline through DWM (Windows 11). The colour is opaque; the system
        /// draws it in the non-client frame, so it never crosses the client content.
        /// </summary>
        public static void ApplyBorderColor(IntPtr hWnd, System.Drawing.Color color)
        {
            const int dwmBorderColor = 34;
            var colorRef = color.R | (color.G << 8) | (color.B << 16);
            DropShadow.DwmSetWindowAttribute(hWnd, dwmBorderColor, ref colorRef, sizeof(int));
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Starts a native window move from a custom title bar. Call from MouseDown; the mouse is
        /// captured by the caller and released by the window manager.
        /// </summary>
        public static void DragWindow(IntPtr hWnd)
        {
            ReleaseCapture();
            SendMessage(hWnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        #region Window styles
        [Flags]
        public enum ExtendedWindowStyles
        {
            // ...
            WS_EX_TOOLWINDOW = 0x00000080,
            // ...
        }

        public enum GetWindowLongFields
        {
            // ...
            GWL_EXSTYLE = (-20),
            // ...
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            int error = 0;
            IntPtr result = IntPtr.Zero;
            // Win32 SetWindowLong doesn't clear error on success
            SetLastError(0);

            if (IntPtr.Size == 4)
            {
                // use SetWindowLong
                Int32 tempResult = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
                error = Marshal.GetLastWin32Error();
                result = new IntPtr(tempResult);
            }
            else
            {
                // use SetWindowLongPtr
                result = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
                error = Marshal.GetLastWin32Error();
            }

            if ((result == IntPtr.Zero) && (error != 0))
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            return result;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

        private static int IntPtrToInt32(IntPtr intPtr)
        {
            return unchecked((int)intPtr.ToInt64());
        }

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        public static extern void SetLastError(int dwErrorCode);
        #endregion

        //allows the context menu to be in dark mode (1), or force dark mode (2)
        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int SetPreferredAppMode(int preferredAppMode);

        public static void HideFromAltTab(IntPtr Handle)
        {
            SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            int exStyle = (int)GetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE);
            exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
        }
    }
}

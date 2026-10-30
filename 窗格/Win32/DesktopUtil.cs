using System;
using System.Runtime.InteropServices;

namespace 窗格.Win32
{
    public class DesktopUtil
    {
        private const Int32 GWL_STYLE = -16;
        private const Int32 GWL_HWNDPARENT = -8;
        private const Int32 WS_MAXIMIZEBOX = 0x00010000;
        private const Int32 WS_MINIMIZEBOX = 0x00020000;

        [DllImport("User32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, Int32 nIndex);

        [DllImport("User32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, Int32 nIndex, IntPtr dwNewLong);

        [DllImport("User32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern Int32 GetWindowLong32(IntPtr hWnd, Int32 nIndex);

        [DllImport("User32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern Int32 SetWindowLong32(IntPtr hWnd, Int32 nIndex, Int32 dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        public static void PreventMinimize(IntPtr handle)
        {
            var windowStyle = GetWindowLongPtr(handle, GWL_STYLE).ToInt64();
            windowStyle &= ~(long)WS_MAXIMIZEBOX;
            windowStyle &= ~(long)WS_MINIMIZEBOX;
            SetWindowLongPtr(handle, GWL_STYLE, new IntPtr(windowStyle));
        }

        public static void GlueToDesktop(IntPtr handle)
        {
            var desktopWindow = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            SetWindowLongPtr(handle, GWL_HWNDPARENT, desktopWindow);
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, Int32 nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, Int32 nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
    }
}

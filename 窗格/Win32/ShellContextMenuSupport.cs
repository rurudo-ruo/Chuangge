using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace 窗格.Win32
{
    public class ShellContextMenuException : Exception
    {
        public ShellContextMenuException()
        {
        }

        public ShellContextMenuException(string message)
            : base(message)
        {
        }
    }

    public class HookEventArgs : EventArgs
    {
        public int HookCode;
        public IntPtr wParam;
        public IntPtr lParam;
    }

    public enum HookType : int
    {
        WH_JOURNALRECORD = 0,
        WH_JOURNALPLAYBACK = 1,
        WH_KEYBOARD = 2,
        WH_GETMESSAGE = 3,
        WH_CALLWNDPROC = 4,
        WH_CBT = 5,
        WH_SYSMSGFILTER = 6,
        WH_MOUSE = 7,
        WH_HARDWARE = 8,
        WH_DEBUG = 9,
        WH_SHELL = 10,
        WH_FOREGROUNDIDLE = 11,
        WH_CALLWNDPROCRET = 12,
        WH_KEYBOARD_LL = 13,
        WH_MOUSE_LL = 14
    }

    public class LocalWindowsHook
    {
        public delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);
        public delegate void HookEventHandler(object sender, HookEventArgs e);

        protected IntPtr m_hhook = IntPtr.Zero;
        protected HookProc m_filterFunc;
        protected HookType m_hookType;

        public event HookEventHandler HookInvoked;

        protected void OnHookInvoked(HookEventArgs e)
        {
            HookInvoked?.Invoke(this, e);
        }

        public LocalWindowsHook(HookType hook)
        {
            m_hookType = hook;
            m_filterFunc = new HookProc(CoreHookProc);
        }

        public LocalWindowsHook(HookType hook, HookProc func)
        {
            m_hookType = hook;
            m_filterFunc = func;
        }

        protected int CoreHookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0)
                return CallNextHookEx(m_hhook, code, wParam, lParam);

            OnHookInvoked(new HookEventArgs
            {
                HookCode = code,
                wParam = wParam,
                lParam = lParam
            });
            return CallNextHookEx(m_hhook, code, wParam, lParam);
        }

        public void Install()
        {
            m_hhook = SetWindowsHookEx(
                m_hookType,
                m_filterFunc,
                IntPtr.Zero,
                (int)Thread.CurrentThread.ManagedThreadId);
        }

        public void Uninstall()
        {
            UnhookWindowsHookEx(m_hhook);
        }

        [DllImport("user32.dll")]
        protected static extern IntPtr SetWindowsHookEx(HookType code, HookProc func, IntPtr hInstance, int threadID);

        [DllImport("user32.dll")]
        protected static extern int UnhookWindowsHookEx(IntPtr hhook);

        [DllImport("user32.dll")]
        protected static extern int CallNextHookEx(IntPtr hhook, int code, IntPtr wParam, IntPtr lParam);
    }

    internal static class ShellHelper
    {
        public static uint HiWord(IntPtr ptr)
        {
            return ((uint)ptr >> 16) & 0xffff;
        }

        public static uint LoWord(IntPtr ptr)
        {
            return (uint)ptr & 0xffff;
        }
    }
}

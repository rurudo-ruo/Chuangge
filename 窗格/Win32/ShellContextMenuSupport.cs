using System;

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

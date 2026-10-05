using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GCode_Sender
{
    /// <summary>
    /// Toggles the Windows DWM "immersive dark mode" attribute on a <see cref="Window"/>
    /// so its non-client area (title bar / borders) follows the application theme.
    /// Requires Windows 10 build 17763 or later. Silently no-ops on older Windows.
    /// </summary>
    internal static class WindowDarkMode
    {
        // Win10 1809 (17763) used 19; Win10 2004 (19041) and later use 20.
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        public static void Apply(Window window, bool dark)
        {
            if (window == null)
                return;

            var helper = new WindowInteropHelper(window);
            // Force HWND creation if needed.
            IntPtr hwnd = helper.EnsureHandle();
            if (hwnd == IntPtr.Zero)
                return;

            int useDark = dark ? 1 : 0;
            try
            {
                int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
                if (hr != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));

                // Force the non-client area to repaint with the new theme.
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WindowDarkMode.Apply failed: " + ex.Message);
            }
        }
    }
}

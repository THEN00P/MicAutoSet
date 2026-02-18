using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI.Xaml;

namespace DontTouchMyMic.Utils
{
    internal static class WindowScaleHelper
    {
        private const uint BaseDpi = 96;

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        public static SizeInt32 ScaleSizeForWindow(Window window, SizeInt32 logicalSize)
        {
            return new SizeInt32(
                ScaleLengthForWindow(window, logicalSize.Width),
                ScaleLengthForWindow(window, logicalSize.Height)
            );
        }

        public static int ScaleLengthForWindow(Window window, int logicalLength)
        {
            if (logicalLength == 0)
            {
                return 0;
            }

            var dpi = GetWindowDpi(window);
            var scaledLength = (int)Math.Round(logicalLength * (dpi / (double)BaseDpi), MidpointRounding.AwayFromZero);

            return logicalLength > 0
                ? Math.Max(1, scaledLength)
                : Math.Min(-1, scaledLength);
        }

        private static uint GetWindowDpi(Window window)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                var systemDpi = GetDpiForSystem();
                return systemDpi == 0 ? BaseDpi : systemDpi;
            }

            var dpi = GetDpiForWindow(hwnd);
            return dpi == 0 ? BaseDpi : dpi;
        }
    }
}

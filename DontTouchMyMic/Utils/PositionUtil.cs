using System.Runtime.InteropServices;
using System.Drawing;
using System;

namespace DontTouchMyMic.Utils
{
    internal class PositionUtil
    {
        private const int AbmGetTaskbarPos = 0x00000005;

        public enum TaskbarEdge : uint
        {
            Left = 0,
            Top = 1,
            Right = 2,
            Bottom = 3
        }

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        public static RECT GetTaskbarRect()
        {
            RECT rect;
            GetWindowRect(FindWindow("Shell_traywnd", ""), out rect);
            return rect;
        }

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);

            return lpPoint;
        }

        public static TaskbarEdge GetTaskbarEdge()
        {
            var appBarData = new APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<APPBARDATA>()
            };

            if (SHAppBarMessage(AbmGetTaskbarPos, ref appBarData) != IntPtr.Zero)
            {
                return (TaskbarEdge)appBarData.uEdge;
            }

            var taskbarRect = GetTaskbarRect();
            var taskbarWidth = taskbarRect.Right - taskbarRect.Left;
            var taskbarHeight = taskbarRect.Bottom - taskbarRect.Top;

            if (taskbarWidth >= taskbarHeight)
            {
                return taskbarRect.Top <= 0 ? TaskbarEdge.Top : TaskbarEdge.Bottom;
            }

            return taskbarRect.Left <= 0 ? TaskbarEdge.Left : TaskbarEdge.Right;
        }

    }
}

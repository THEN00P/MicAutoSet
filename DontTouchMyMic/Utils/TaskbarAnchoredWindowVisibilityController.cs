using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using H.NotifyIcon;

namespace DontTouchMyMic.Utils
{
    internal sealed class TaskbarAnchoredWindowVisibilityController : IDisposable
    {
        private readonly Window m_window;
        private readonly int m_windowOffsetFromTaskbar;
        private readonly int m_windowAnimationDurationMs;

        private CancellationTokenSource m_windowAnimationCts;

        public TaskbarAnchoredWindowVisibilityController(Window window, int windowOffsetFromTaskbar, int windowAnimationDurationMs)
        {
            m_window = window;
            m_windowOffsetFromTaskbar = windowOffsetFromTaskbar;
            m_windowAnimationDurationMs = windowAnimationDurationMs;
        }

        public bool IsVisible
        {
            get
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
                return PositionUtil.IsWindowVisible(hwnd);
            }
        }

        public async Task ShowAsync(SizeInt32 windowSize)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
            var targetRect = TaskbarAnchoredWindowAnimation.CalculateVisibleWindowRect(windowSize, m_windowOffsetFromTaskbar);

            if (PositionUtil.IsWindowVisible(hwnd))
            {
                CancelWindowAnimation();
                m_window.AppWindow.MoveAndResize(targetRect);
                WindowExtensions.Show(m_window, true);
                PositionUtil.SetForegroundWindow(hwnd);
                return;
            }

            var animationCts = ReplaceWindowAnimationToken();

            try
            {
                var hiddenRect = TaskbarAnchoredWindowAnimation.CalculateHiddenWindowRect(targetRect);
                m_window.AppWindow.MoveAndResize(hiddenRect);
                WindowExtensions.Show(m_window, true);

                var pinnedBelowTaskbar = PositionUtil.PlaceWindowBelowTaskbarAboveApps(hwnd);
                if (!pinnedBelowTaskbar)
                {
                    PositionUtil.SetForegroundWindow(hwnd);
                }

                await TaskbarAnchoredWindowAnimation.AnimateWindowRectAsync(
                    m_window.AppWindow,
                    hiddenRect,
                    targetRect,
                    m_windowAnimationDurationMs,
                    animationCts.Token
                );

                PositionUtil.SetForegroundWindow(hwnd);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                DisposeWindowAnimationToken(animationCts);
            }
        }

        public async Task<bool> HideAsync()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
            if (!PositionUtil.IsWindowVisible(hwnd))
            {
                return false;
            }

            var animationCts = ReplaceWindowAnimationToken();

            try
            {
                var currentRect = new RectInt32(
                    m_window.AppWindow.Position.X,
                    m_window.AppWindow.Position.Y,
                    m_window.AppWindow.Size.Width,
                    m_window.AppWindow.Size.Height
                );

                var hiddenRect = TaskbarAnchoredWindowAnimation.CalculateHiddenWindowRect(currentRect);
                var pinnedBelowTaskbar = PositionUtil.PlaceWindowBelowTaskbarAboveApps(hwnd);
                if (!pinnedBelowTaskbar)
                {
                    PositionUtil.SetForegroundWindow(hwnd);
                }

                await TaskbarAnchoredWindowAnimation.AnimateWindowRectAsync(
                    m_window.AppWindow,
                    currentRect,
                    hiddenRect,
                    m_windowAnimationDurationMs,
                    animationCts.Token
                );

                WindowExtensions.Hide(m_window, true);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                PositionUtil.EnsureWindowNotTopMost(hwnd);
                DisposeWindowAnimationToken(animationCts);
            }
        }

        public void Dispose()
        {
            CancelWindowAnimation();
        }

        private CancellationTokenSource ReplaceWindowAnimationToken()
        {
            CancelWindowAnimation();
            m_windowAnimationCts = new CancellationTokenSource();
            return m_windowAnimationCts;
        }

        private void DisposeWindowAnimationToken(CancellationTokenSource token)
        {
            if (ReferenceEquals(m_windowAnimationCts, token))
            {
                m_windowAnimationCts = null;
            }

            token.Dispose();
        }

        private void CancelWindowAnimation()
        {
            if (m_windowAnimationCts == null)
            {
                return;
            }

            m_windowAnimationCts.Cancel();
            m_windowAnimationCts.Dispose();
            m_windowAnimationCts = null;
        }
    }
}

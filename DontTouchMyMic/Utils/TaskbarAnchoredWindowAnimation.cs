using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

namespace DontTouchMyMic.Utils
{
    internal static class TaskbarAnchoredWindowAnimation
    {
        public static RectInt32 CalculateVisibleWindowRect(SizeInt32 windowSize, int windowOffsetFromTaskbar)
        {
            var cursorPosition = PositionUtil.GetCursorPosition();
            var displayArea = DisplayArea.GetFromPoint(new PointInt32(cursorPosition.X, cursorPosition.Y), DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            var taskbarEdge = PositionUtil.GetTaskbarEdge();

            var centeredX = cursorPosition.X - (windowSize.Width / 2);
            var centeredY = cursorPosition.Y - (windowSize.Height / 2);
            var clampedX = Clamp(centeredX, workArea.X, workArea.X + workArea.Width - windowSize.Width);
            var clampedY = Clamp(centeredY, workArea.Y, workArea.Y + workArea.Height - windowSize.Height);

            return taskbarEdge switch
            {
                PositionUtil.TaskbarEdge.Top =>
                    new RectInt32(
                        clampedX,
                        workArea.Y + windowOffsetFromTaskbar,
                        windowSize.Width,
                        windowSize.Height
                    ),
                PositionUtil.TaskbarEdge.Left =>
                    new RectInt32(
                        workArea.X + windowOffsetFromTaskbar,
                        clampedY,
                        windowSize.Width,
                        windowSize.Height
                    ),
                PositionUtil.TaskbarEdge.Right =>
                    new RectInt32(
                        workArea.X + workArea.Width - windowSize.Width - windowOffsetFromTaskbar,
                        clampedY,
                        windowSize.Width,
                        windowSize.Height
                    ),
                _ =>
                    new RectInt32(
                        clampedX,
                        workArea.Y + workArea.Height - windowSize.Height - windowOffsetFromTaskbar,
                        windowSize.Width,
                        windowSize.Height
                    )
            };
        }

        public static RectInt32 CalculateHiddenWindowRect(RectInt32 visibleRect)
        {
            var centerPoint = new PointInt32(
                visibleRect.X + (visibleRect.Width / 2),
                visibleRect.Y + (visibleRect.Height / 2)
            );

            var displayArea = DisplayArea.GetFromPoint(centerPoint, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            var taskbarEdge = PositionUtil.GetTaskbarEdge();

            return taskbarEdge switch
            {
                PositionUtil.TaskbarEdge.Top =>
                    new RectInt32(
                        visibleRect.X,
                        workArea.Y - visibleRect.Height - 1,
                        visibleRect.Width,
                        visibleRect.Height
                    ),
                PositionUtil.TaskbarEdge.Left =>
                    new RectInt32(
                        workArea.X - visibleRect.Width - 1,
                        visibleRect.Y,
                        visibleRect.Width,
                        visibleRect.Height
                    ),
                PositionUtil.TaskbarEdge.Right =>
                    new RectInt32(
                        workArea.X + workArea.Width + 1,
                        visibleRect.Y,
                        visibleRect.Width,
                        visibleRect.Height
                    ),
                _ =>
                    new RectInt32(
                        visibleRect.X,
                        workArea.Y + workArea.Height + 1,
                        visibleRect.Width,
                        visibleRect.Height
                    )
            };
        }

        public static async Task AnimateWindowRectAsync(AppWindow appWindow, RectInt32 fromRect, RectInt32 toRect, int durationMs, CancellationToken cancellationToken)
        {
            if (durationMs <= 0 || RectEquals(fromRect, toRect))
            {
                ApplyWindowRect(appWindow, toRect);
                return;
            }

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopwatch = Stopwatch.StartNew();

            EventHandler<object> renderingHandler = null;
            CancellationTokenRegistration cancellationRegistration = default;

            void CompleteAnimation()
            {
                CompositionTarget.Rendering -= renderingHandler;
                cancellationRegistration.Dispose();
            }

            renderingHandler = (_, _) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    CompleteAnimation();
                    tcs.TrySetCanceled(cancellationToken);
                    return;
                }

                var rawProgress = stopwatch.Elapsed.TotalMilliseconds / durationMs;
                if (rawProgress >= 1)
                {
                    ApplyWindowRect(appWindow, toRect);
                    CompleteAnimation();
                    tcs.TrySetResult(null);
                    return;
                }

                var easedProgress = 1 - Math.Pow(1 - rawProgress, 3);
                ApplyWindowRect(appWindow, LerpRect(fromRect, toRect, easedProgress));
            };

            cancellationRegistration = cancellationToken.Register(() =>
            {
                CompleteAnimation();
                tcs.TrySetCanceled(cancellationToken);
            });

            CompositionTarget.Rendering += renderingHandler;

            await tcs.Task;
        }

        private static void ApplyWindowRect(AppWindow appWindow, RectInt32 rect)
        {
            if (appWindow.Size.Width == rect.Width && appWindow.Size.Height == rect.Height)
            {
                appWindow.Move(new PointInt32(rect.X, rect.Y));
                return;
            }

            appWindow.MoveAndResize(rect);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static RectInt32 LerpRect(RectInt32 fromRect, RectInt32 toRect, double progress)
        {
            return new RectInt32(
                LerpInt(fromRect.X, toRect.X, progress),
                LerpInt(fromRect.Y, toRect.Y, progress),
                LerpInt(fromRect.Width, toRect.Width, progress),
                LerpInt(fromRect.Height, toRect.Height, progress)
            );
        }

        private static int LerpInt(int from, int to, double progress)
        {
            return from + (int)Math.Round((to - from) * progress);
        }

        private static bool RectEquals(RectInt32 first, RectInt32 second)
        {
            return first.X == second.X && first.Y == second.Y && first.Width == second.Width && first.Height == second.Height;
        }
    }
}

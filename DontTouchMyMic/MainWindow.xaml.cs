using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT;
using Microsoft.UI.Windowing;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Media.Animation;
using DontTouchMyMic.Utils;
using DontTouchMyMic.Pages;


namespace DontTouchMyMic
{
    public sealed partial class MainWindow : Window
    {
        private const int WindowAnimationDurationMs = 120;
        private const int WindowOffsetFromTaskbar = 5;

        WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController m_acrylicController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;
        CancellationTokenSource m_windowAnimationCts;
        AboutWindow m_aboutWindow;

        public MainWindow()
        {
            InitializeComponent();

            var presenter = AppWindow.Presenter as OverlappedPresenter;

            if (presenter != null)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            SetWindowDimensions(MainPage.PageSize);
            ContentFrame.Navigate(typeof(MainPage));
            
            TrySetAcrylicBackdrop(false);
        }
        
        public void NavigateTo(Type sourcePageType)
        {
            ContentFrame.Navigate(sourcePageType, null, new SlideNavigationTransitionInfo {Effect = SlideNavigationTransitionEffect.FromRight});
        }
        
        public void NavigateBack()
        {
            ContentFrame.GoBack();
        }

        private async void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
#if !DEBUG
                await HideWindowAnimatedAsync(navigateToMainPageAfterHide: true);
#endif
            }
        }
        
        [RelayCommand]
        public Task OpenWindow()
        {
            return OpenWindowAnimatedAsync();
        }

        [RelayCommand]
        public void OpenAboutWindow()
        {
            if (m_aboutWindow == null)
            {
                m_aboutWindow = new AboutWindow();
                m_aboutWindow.Closed += AboutWindow_Closed;
            }

            m_aboutWindow.CenterOnScreen();
            m_aboutWindow.Activate();
        }

        private async Task OpenWindowAnimatedAsync()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var targetRect = CalculateVisibleWindowRect(MainPage.PageSize);
            var isWindowVisible = PositionUtil.IsWindowVisible(hwnd);

            if (isWindowVisible)
            {
                CancelWindowAnimation();
                AppWindow.MoveAndResize(targetRect);
                WindowExtensions.Show(this, true);
                PositionUtil.SetForegroundWindow(hwnd);
                return;
            }

            var animationCts = ReplaceWindowAnimationToken();

            try
            {
                var hiddenRect = CalculateHiddenWindowRect(targetRect);
                AppWindow.MoveAndResize(hiddenRect);
                WindowExtensions.Show(this, true);
                PositionUtil.SetForegroundWindow(hwnd);

                await AnimateWindowRectAsync(hiddenRect, targetRect, WindowAnimationDurationMs, animationCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                DisposeWindowAnimationToken(animationCts);
            }
        }

        [RelayCommand]
        public void ExitApplication()
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            if (m_aboutWindow != null)
            {
                m_aboutWindow.Closed -= AboutWindow_Closed;
                m_aboutWindow.Close();
                m_aboutWindow = null;
            }

            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
            m_configurationSource = null;
        }

        private void AboutWindow_Closed(object sender, WindowEventArgs args)
        {
            if (sender is AboutWindow aboutWindow)
            {
                aboutWindow.Closed -= AboutWindow_Closed;
            }

            m_aboutWindow = null;
        }

        private void SetWindowDimensions(SizeInt32 windowSize)
        {
            this.AppWindow.MoveAndResize(CalculateVisibleWindowRect(windowSize));
        }

        private async Task HideWindowAnimatedAsync(bool navigateToMainPageAfterHide)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (!PositionUtil.IsWindowVisible(hwnd))
            {
                if (navigateToMainPageAfterHide)
                {
                    ContentFrame.Navigate(typeof(MainPage));
                }
                return;
            }

            var animationCts = ReplaceWindowAnimationToken();

            try
            {
                var currentRect = new RectInt32(
                    AppWindow.Position.X,
                    AppWindow.Position.Y,
                    AppWindow.Size.Width,
                    AppWindow.Size.Height
                );

                var hiddenRect = CalculateHiddenWindowRect(currentRect);
                var pinnedBelowTaskbar = PositionUtil.PlaceWindowBelowTaskbarAboveApps(hwnd);
                if (!pinnedBelowTaskbar)
                {
                    PositionUtil.SetForegroundWindow(hwnd);
                }

                await AnimateWindowRectAsync(currentRect, hiddenRect, WindowAnimationDurationMs, animationCts.Token);
                WindowExtensions.Hide(this, true);

                if (navigateToMainPageAfterHide)
                {
                    ContentFrame.Navigate(typeof(MainPage));
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                PositionUtil.EnsureWindowNotTopMost(hwnd);
                DisposeWindowAnimationToken(animationCts);
            }
        }

        private RectInt32 CalculateVisibleWindowRect(SizeInt32 windowSize)
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
                        workArea.Y + WindowOffsetFromTaskbar,
                        windowSize.Width,
                        windowSize.Height
                    ),
                PositionUtil.TaskbarEdge.Left =>
                    new RectInt32(
                        workArea.X + WindowOffsetFromTaskbar,
                        clampedY,
                        windowSize.Width,
                        windowSize.Height
                    ),
                PositionUtil.TaskbarEdge.Right =>
                    new RectInt32(
                        workArea.X + workArea.Width - windowSize.Width - WindowOffsetFromTaskbar,
                        clampedY,
                        windowSize.Width,
                        windowSize.Height
                    ),
                _ =>
                    new RectInt32(
                        clampedX,
                        workArea.Y + workArea.Height - windowSize.Height - WindowOffsetFromTaskbar,
                        windowSize.Width,
                        windowSize.Height
                    )
            };
        }

        private RectInt32 CalculateHiddenWindowRect(RectInt32 visibleRect)
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

        private async Task AnimateWindowRectAsync(RectInt32 fromRect, RectInt32 toRect, int durationMs, CancellationToken cancellationToken)
        {
            if (durationMs <= 0 || RectEquals(fromRect, toRect))
            {
                ApplyWindowRect(toRect);
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
                    ApplyWindowRect(toRect);
                    CompleteAnimation();
                    tcs.TrySetResult(null);
                    return;
                }

                var easedProgress = 1 - Math.Pow(1 - rawProgress, 3);
                ApplyWindowRect(LerpRect(fromRect, toRect, easedProgress));
            };

            cancellationRegistration = cancellationToken.Register(() =>
            {
                CompleteAnimation();
                tcs.TrySetCanceled(cancellationToken);
            });

            CompositionTarget.Rendering += renderingHandler;

            await tcs.Task;
        }

        private void ApplyWindowRect(RectInt32 rect)
        {
            if (AppWindow.Size.Width == rect.Width && AppWindow.Size.Height == rect.Height)
            {
                AppWindow.Move(new PointInt32(rect.X, rect.Y));
                return;
            }

            AppWindow.MoveAndResize(rect);
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

        bool TrySetAcrylicBackdrop(bool useAcrylicThin)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
                m_configurationSource.IsInputActive = true;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();

                m_acrylicController.Kind = useAcrylicThin ? Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicKind.Thin : Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicKind.Base;

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_acrylicController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // Succeeded.
            }

            return false; // Acrylic is not supported on this system.
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
        }
    }
}

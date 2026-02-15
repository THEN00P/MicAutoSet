using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics;
using Microsoft.UI.Xaml;
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
        private const int NavigationContentAnimationDurationMs = 150;
        private const double NavigationContentSlideOffsetPx = 22;
        private const int WindowOffsetFromTaskbar = 5;

        TaskbarAnchoredWindowVisibilityController m_visibilityController;
        WindowAcrylicBackdrop m_acrylicBackdrop;
        AboutWindow m_aboutWindow;
        TrayMenuWindow m_trayMenuWindow;
        Storyboard m_navigationContentStoryboard;

        public MainWindow()
        {
            InitializeComponent();
            Closed += Window_Closed;

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

            m_visibilityController = new TaskbarAnchoredWindowVisibilityController(this, WindowOffsetFromTaskbar, WindowAnimationDurationMs);
            m_acrylicBackdrop = new WindowAcrylicBackdrop(this);
            m_acrylicBackdrop.TryEnable(useAcrylicThin: false);
        }
        
        public void NavigateTo(Type destinationPageType)
        {
            NavigateCore(destinationPageType, isBackNavigation: false);
        }
        
        public void NavigateBack()
        {
            if (!ContentFrame.CanGoBack)
            {
                return;
            }

            var previousPageType = ContentFrame.BackStack.Last().SourcePageType;
            NavigateCore(previousPageType, isBackNavigation: true);
        }

        private async void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                await HideWindowAnimatedAsync(navigateToMainPageAfterHide: true);
            }
        }
        
        [RelayCommand]
        public Task OpenWindow()
        {
            return OpenWindowAnimatedAsync();
        }

        [RelayCommand]
        public async Task OpenTrayMenu()
        {
            var trayMenuWindow = EnsureTrayMenuWindow();
            await trayMenuWindow.ToggleWindowAsync();
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

        private TrayMenuWindow EnsureTrayMenuWindow()
        {
            if (m_trayMenuWindow != null)
            {
                return m_trayMenuWindow;
            }

            m_trayMenuWindow = new TrayMenuWindow(this);
            m_trayMenuWindow.Closed += TrayMenuWindow_Closed;
            m_trayMenuWindow.Activate();
            WindowExtensions.Hide(m_trayMenuWindow, true);
            return m_trayMenuWindow;
        }

        private async Task OpenWindowAnimatedAsync()
        {
            await m_visibilityController.ShowAsync(MainPage.PageSize);
        }

        [RelayCommand]
        public void ExitApplication()
        {
            if (m_trayMenuWindow != null)
            {
                m_trayMenuWindow.Closed -= TrayMenuWindow_Closed;
                m_trayMenuWindow.Close();
                m_trayMenuWindow = null;
            }

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

            if (m_trayMenuWindow != null)
            {
                m_trayMenuWindow.Closed -= TrayMenuWindow_Closed;
                m_trayMenuWindow.Close();
                m_trayMenuWindow = null;
            }

            m_visibilityController?.Dispose();
            m_visibilityController = null;

            m_acrylicBackdrop?.Dispose();
            m_acrylicBackdrop = null;
        }

        private void AboutWindow_Closed(object sender, WindowEventArgs args)
        {
            if (sender is AboutWindow aboutWindow)
            {
                aboutWindow.Closed -= AboutWindow_Closed;
            }

            m_aboutWindow = null;
        }

        private void TrayMenuWindow_Closed(object sender, WindowEventArgs args)
        {
            if (sender is TrayMenuWindow trayMenuWindow)
            {
                trayMenuWindow.Closed -= TrayMenuWindow_Closed;
            }

            m_trayMenuWindow = null;
        }

        private void SetWindowDimensions(SizeInt32 windowSize)
        {
            AppWindow.MoveAndResize(TaskbarAnchoredWindowAnimation.CalculateVisibleWindowRect(windowSize, WindowOffsetFromTaskbar));
        }

        private void NavigateCore(Type destinationPageType, bool isBackNavigation)
        {
            ResizeWindowForPage(destinationPageType);

            if (isBackNavigation)
            {
                ContentFrame.GoBack(new SuppressNavigationTransitionInfo());
            }
            else
            {
                ContentFrame.Navigate(destinationPageType, null, new SuppressNavigationTransitionInfo());
            }

            PlayNavigationContentAnimation(isBackNavigation);
        }

        private void ResizeWindowForPage(Type pageType)
        {
            if (!TryGetPageSize(pageType, out var targetSize))
            {
                return;
            }

            var currentPosition = AppWindow.Position;
            var currentSize = AppWindow.Size;

            if (currentSize.Width == targetSize.Width && currentSize.Height == targetSize.Height)
            {
                return;
            }

            var targetY = currentPosition.Y + currentSize.Height - targetSize.Height;
            AppWindow.MoveAndResize(new RectInt32(currentPosition.X, targetY, targetSize.Width, targetSize.Height));
        }

        private static bool TryGetPageSize(Type pageType, out SizeInt32 pageSize)
        {
            if (pageType == typeof(MainPage))
            {
                pageSize = MainPage.PageSize;
                return true;
            }

            if (pageType == typeof(SelectorPage))
            {
                pageSize = SelectorPage.PageSize;
                return true;
            }

            pageSize = default;
            return false;
        }

        private void PlayNavigationContentAnimation(bool isBackNavigation)
        {
            m_navigationContentStoryboard?.Stop();

            var slideFromX = isBackNavigation ? -NavigationContentSlideOffsetPx : NavigationContentSlideOffsetPx;

            ContentFrameTranslateTransform.X = slideFromX;
            ContentFrame.Opacity = 0.92;

            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var slideAnimation = new DoubleAnimation
            {
                From = slideFromX,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(NavigationContentAnimationDurationMs),
                EnableDependentAnimation = true,
                EasingFunction = easing
            };

            var fadeAnimation = new DoubleAnimation
            {
                From = 0.92,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(NavigationContentAnimationDurationMs),
                EasingFunction = easing
            };

            Storyboard.SetTarget(slideAnimation, ContentFrameTranslateTransform);
            Storyboard.SetTargetProperty(slideAnimation, "X");

            Storyboard.SetTarget(fadeAnimation, ContentFrame);
            Storyboard.SetTargetProperty(fadeAnimation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(slideAnimation);
            storyboard.Children.Add(fadeAnimation);
            storyboard.Completed += (_, _) =>
            {
                ContentFrameTranslateTransform.X = 0;
                ContentFrame.Opacity = 1;
            };

            m_navigationContentStoryboard = storyboard;
            storyboard.Begin();
        }

        private async Task HideWindowAnimatedAsync(bool navigateToMainPageAfterHide)
        {
            if (!m_visibilityController.IsVisible)
            {
                if (navigateToMainPageAfterHide)
                {
                    ContentFrame.Navigate(typeof(MainPage));
                }
                return;
            }

            var wasHidden = await m_visibilityController.HideAsync();
            if (wasHidden && navigateToMainPageAfterHide)
            {
                ContentFrame.Navigate(typeof(MainPage));
            }
        }
    }
}

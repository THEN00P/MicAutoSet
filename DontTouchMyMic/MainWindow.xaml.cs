using System;
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
        private const int WindowOffsetFromTaskbar = 5;

        TaskbarAnchoredWindowVisibilityController m_visibilityController;
        WindowAcrylicBackdrop m_acrylicBackdrop;
        AboutWindow m_aboutWindow;
        TrayMenuWindow m_trayMenuWindow;

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

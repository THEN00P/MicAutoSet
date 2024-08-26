using System;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using WinRT;
using Microsoft.UI.Windowing;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Media.Animation;
using MicVolumeLock.Pages;


namespace MicVolumeLock
{
    public sealed partial class MainWindow : Window
    {
        OverlappedPresenter presenter;
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See separate sample below for implementation
        Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController m_acrylicController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

        public MainWindow()
        {
            InitializeComponent();

            presenter = AppWindow.Presenter as OverlappedPresenter;
            
            if (presenter != null)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            SetWindowDimensions(MainPage.PageSize);
            NavigateTo(typeof(MainPage));
            
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

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
#if !DEBUG
                WindowExtensions.Hide(this, true);
#endif
            }
        }

        [RelayCommand]
        public void OpenWindow()
        {
            SetWindowDimensions(MainPage.PageSize);
            NavigateTo(typeof(MainPage));
            WindowExtensions.Show(this, true);
            Util.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        }

        [RelayCommand]
        public void ExitApplication()
        {
            this.Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
            m_configurationSource = null;
        }

        private void SetWindowDimensions(Windows.Graphics.SizeInt32 windowSize)
        {
            var curPos = Util.GetCursorPosition();

            var winX = curPos.X - (windowSize.Width / 2);
            if (winX < 0)
            {
                winX = 0;
            }

            var winY = curPos.Y - windowSize.Height;
            if (winY < 0)
            {
                winY = 0;
            }
            
            this.AppWindow.MoveAndResize(new RectInt32(winX, winY, windowSize.Width, windowSize.Height));
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

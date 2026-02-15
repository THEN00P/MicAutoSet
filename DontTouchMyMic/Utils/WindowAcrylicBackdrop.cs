using System;
using Microsoft.UI.Xaml;
using WinRT;

namespace DontTouchMyMic.Utils
{
    internal sealed class WindowAcrylicBackdrop : IDisposable
    {
        private readonly Window m_window;

        private WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        private Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController m_acrylicController;
        private Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;
        private FrameworkElement m_themeSource;

        public WindowAcrylicBackdrop(Window window)
        {
            m_window = window;
        }

        public bool TryEnable(bool useAcrylicThin)
        {
            if (!Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                return false;
            }

            m_wsdqHelper ??= new WindowsSystemDispatcherQueueHelper();
            m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

            m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration
            {
                IsInputActive = true
            };

            AttachThemeSource();
            SetConfigurationSourceTheme();

            m_acrylicController?.Dispose();
            m_acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController
            {
                Kind = useAcrylicThin
                    ? Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicKind.Thin
                    : Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicKind.Base
            };

            m_acrylicController.AddSystemBackdropTarget(m_window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            m_acrylicController.SetSystemBackdropConfiguration(m_configurationSource);
            return true;
        }

        public void Dispose()
        {
            if (m_themeSource != null)
            {
                m_themeSource.ActualThemeChanged -= ThemeSource_ActualThemeChanged;
                m_themeSource = null;
            }

            m_acrylicController?.Dispose();
            m_acrylicController = null;
            m_configurationSource = null;
        }

        private void AttachThemeSource()
        {
            if (m_themeSource != null)
            {
                m_themeSource.ActualThemeChanged -= ThemeSource_ActualThemeChanged;
            }

            m_themeSource = m_window.Content as FrameworkElement;
            if (m_themeSource != null)
            {
                m_themeSource.ActualThemeChanged += ThemeSource_ActualThemeChanged;
            }
        }

        private void ThemeSource_ActualThemeChanged(FrameworkElement sender, object args)
        {
            SetConfigurationSourceTheme();
        }

        private void SetConfigurationSourceTheme()
        {
            if (m_configurationSource == null || m_themeSource == null)
            {
                return;
            }

            m_configurationSource.Theme = m_themeSource.ActualTheme switch
            {
                ElementTheme.Dark => Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark,
                ElementTheme.Light => Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light,
                _ => Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default
            };
        }
    }
}

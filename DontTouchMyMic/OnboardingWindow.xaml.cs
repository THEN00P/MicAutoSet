using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using DontTouchMyMic.Utils;
using H.NotifyIcon;

namespace DontTouchMyMic
{
    public sealed partial class OnboardingWindow : Window
    {
        private const int LastPageIndex = 1;
        private const int PageTransitionDurationMs = 220;
        private const double EnterSlideOffsetPx = 40;
        private const double ExitSlideOffsetPx = 22;
        private const string DarkVideoFileName = "onboarding.dark.mp4";
        private const string LightVideoFileName = "onboarding.light.mp4";
        private const string DarkVideoUri = "ms-appx:///Assets/onboarding.dark.mp4";
        private const string LightVideoUri = "ms-appx:///Assets/onboarding.light.mp4";
        private const string DarkVideoWebUri = "ms-appx-web:///Assets/onboarding.dark.mp4";
        private const string LightVideoWebUri = "ms-appx-web:///Assets/onboarding.light.mp4";
        private static readonly SizeInt32 LogicalWindowSize = new(640, 520);

        private int m_currentPageIndex;
        private string m_currentVideoUri;
        private bool m_isNavigating;
        private bool m_hasRetriedCurrentVideoSource;
        private bool m_allowWindowClose;

        public OnboardingWindow()
        {
            InitializeComponent();

            Title = string.Empty;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarDragRegion);
            SystemBackdrop = new MicaBackdrop();

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            GetOrCreateMediaPlayer().IsLoopingEnabled = true;

            ApplyCurrentPageWithoutAnimation();
            UpdateVideoSourceForTheme(forceReload: true);
            ResizeForCurrentScale();
            CenterOnScreen();
            AppWindow.Closing += AppWindow_Closing;
            Closed += OnboardingWindow_Closed;
        }

        public void CenterOnScreen()
        {
            ResizeForCurrentScale();

            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            var width = AppWindow.Size.Width;
            var height = AppWindow.Size.Height;

            var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

            AppWindow.Move(new PointInt32(x, y));
        }

        private void ResizeForCurrentScale()
        {
            AppWindow.Resize(WindowScaleHelper.ScaleSizeForWindow(this, LogicalWindowSize));
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (m_isNavigating)
            {
                return;
            }

            if (m_currentPageIndex < LastPageIndex)
            {
                await NavigateToPageAsync(m_currentPageIndex + 1);
                return;
            }

            NextButton.IsEnabled = false;

            try
            {
                var enableRunOnStartup = RunOnStartupCheckBox.IsChecked != false;
                await AutoStartManager.SetEnabledAsync(enableRunOnStartup);
            }
            catch
            {
                // Do not terminate onboarding flow if startup-task registration is unavailable.
            }
            finally
            {
                DismissOnboardingWindow();
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (m_isNavigating || m_currentPageIndex == 0)
            {
                return;
            }

            await NavigateToPageAsync(m_currentPageIndex - 1);
        }

        private async Task NavigateToPageAsync(int targetPageIndex)
        {
            if (targetPageIndex < 0 || targetPageIndex > LastPageIndex || targetPageIndex == m_currentPageIndex || m_isNavigating)
            {
                return;
            }

            m_isNavigating = true;
            BackButton.IsEnabled = false;
            NextButton.IsEnabled = false;

            var previousPageIndex = m_currentPageIndex;
            var isForwardNavigation = targetPageIndex > previousPageIndex;

            var fromPage = GetPage(previousPageIndex);
            var toPage = GetPage(targetPageIndex);
            var fromTransform = GetTranslateTransform(fromPage);
            var toTransform = GetTranslateTransform(toPage);

            toPage.Visibility = Visibility.Visible;
            toPage.Opacity = 0;
            toTransform.X = isForwardNavigation ? EnterSlideOffsetPx : -EnterSlideOffsetPx;
            fromTransform.X = 0;

            if (previousPageIndex == 1 && targetPageIndex != 1)
            {
                GetOrCreateMediaPlayer().Pause();
            }

            if (targetPageIndex == 1)
            {
                UpdateVideoSourceForTheme(forceReload: false);
            }

            try
            {
                await PlayPageTransitionAsync(fromPage, toPage, fromTransform, toTransform, isForwardNavigation);
                fromPage.Visibility = Visibility.Collapsed;
                fromPage.Opacity = 1;
                fromTransform.X = 0;
                toPage.Opacity = 1;
                toTransform.X = 0;

                m_currentPageIndex = targetPageIndex;
                UpdateStepUi();
            }
            finally
            {
                m_isNavigating = false;
                BackButton.IsEnabled = true;
                NextButton.IsEnabled = true;
            }
        }

        private async Task PlayPageTransitionAsync(
            FrameworkElement fromPage,
            FrameworkElement toPage,
            TranslateTransform fromTransform,
            TranslateTransform toTransform,
            bool isForwardNavigation)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(PageTransitionDurationMs));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var exitOffset = isForwardNavigation ? -ExitSlideOffsetPx : ExitSlideOffsetPx;

            var fromSlideAnimation = new DoubleAnimation
            {
                From = 0,
                To = exitOffset,
                Duration = duration,
                EasingFunction = easing,
                EnableDependentAnimation = true
            };

            var toSlideAnimation = new DoubleAnimation
            {
                From = toTransform.X,
                To = 0,
                Duration = duration,
                EasingFunction = easing,
                EnableDependentAnimation = true
            };

            var fromFadeAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };

            var toFadeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = easing
            };

            Storyboard.SetTarget(fromSlideAnimation, fromTransform);
            Storyboard.SetTargetProperty(fromSlideAnimation, "X");
            Storyboard.SetTarget(toSlideAnimation, toTransform);
            Storyboard.SetTargetProperty(toSlideAnimation, "X");
            Storyboard.SetTarget(fromFadeAnimation, fromPage);
            Storyboard.SetTargetProperty(fromFadeAnimation, "Opacity");
            Storyboard.SetTarget(toFadeAnimation, toPage);
            Storyboard.SetTargetProperty(toFadeAnimation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fromSlideAnimation);
            storyboard.Children.Add(toSlideAnimation);
            storyboard.Children.Add(fromFadeAnimation);
            storyboard.Children.Add(toFadeAnimation);

            var transitionCompletion = new TaskCompletionSource<bool>();
            storyboard.Completed += (_, _) => transitionCompletion.TrySetResult(true);
            storyboard.Begin();

            await transitionCompletion.Task;
        }

        private void ApplyCurrentPageWithoutAnimation()
        {
            for (var pageIndex = 0; pageIndex <= LastPageIndex; pageIndex++)
            {
                var page = GetPage(pageIndex);
                var transform = GetTranslateTransform(page);
                transform.X = 0;
                page.Opacity = 1;
                page.Visibility = pageIndex == m_currentPageIndex ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdateStepUi();
        }

        private void UpdateStepUi()
        {
            BackButton.Visibility = m_currentPageIndex == 0 ? Visibility.Collapsed : Visibility.Visible;

            if (m_currentPageIndex == 1)
            {
                UpdateVideoSourceForTheme(forceReload: false);
                GetOrCreateMediaPlayer().Play();
            }
            else
            {
                GetOrCreateMediaPlayer().Pause();
            }

            var isLastPage = m_currentPageIndex == LastPageIndex;
            NextButtonTextBlock.Text = isLastPage ? "Close" : "Next";
            NextButtonIcon.Glyph = isLastPage ? "\uE711" : "\uE72A";
        }

        private void RootGrid_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateVideoSourceForTheme(forceReload: false);
        }

        private void UpdateVideoSourceForTheme(bool forceReload)
        {
            var isDarkTheme = RootGrid.ActualTheme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                _ => Application.Current?.RequestedTheme == ApplicationTheme.Dark
            };

            var targetUri = ResolveVideoSourceUri(isDarkTheme);
            if (!forceReload && string.Equals(targetUri, m_currentVideoUri, StringComparison.Ordinal))
            {
                return;
            }

            m_hasRetriedCurrentVideoSource = false;
            m_currentVideoUri = targetUri;

            var mediaPlayer = GetOrCreateMediaPlayer();
            mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(targetUri));

            if (m_currentPageIndex == 1)
            {
                mediaPlayer.Play();
            }
        }

        private static string ResolveVideoSourceUri(bool isDarkTheme)
        {
            var fileName = isDarkTheme ? DarkVideoFileName : LightVideoFileName;
            var localAssetPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
            if (File.Exists(localAssetPath))
            {
                return new Uri(localAssetPath).AbsoluteUri;
            }

            return isDarkTheme ? DarkVideoUri : LightVideoUri;
        }

        private void VideoContainerBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (VideoContainerBorder == null)
            {
                return;
            }

            var width = e.NewSize.Width;
            if (width <= 0)
            {
                return;
            }

            var horizontalBorderThickness = VideoContainerBorder.BorderThickness.Left + VideoContainerBorder.BorderThickness.Right;
            var verticalBorderThickness = VideoContainerBorder.BorderThickness.Top + VideoContainerBorder.BorderThickness.Bottom;
            var contentWidth = Math.Max(0, width - horizontalBorderThickness);
            var targetHeight = Math.Round((contentWidth * 9.0 / 16.0) + verticalBorderThickness, MidpointRounding.AwayFromZero);
            if (Math.Abs(VideoContainerBorder.Height - targetHeight) > 0.5)
            {
                VideoContainerBorder.Height = targetHeight;
            }
        }

        internal void CloseForAppShutdown()
        {
            m_allowWindowClose = true;
            AppWindow.Closing -= AppWindow_Closing;

            try
            {
                Close();
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x80004004)
            {
                // WinUI can throw E_ABORT during teardown; app shutdown continues via App.Exit().
            }
        }

        private void DismissOnboardingWindow()
        {
            GetOrCreateMediaPlayer().Pause();
            WindowExtensions.Hide(this, true);

            if (App.MainWindow != null)
            {
                WindowExtensions.Show(App.MainWindow, true);
                WindowExtensions.Hide(App.MainWindow, true);
            }
        }

        private static string GetFallbackPackagedVideoUri(bool isDarkTheme)
        {
            return isDarkTheme ? DarkVideoWebUri : LightVideoWebUri;
        }

        private MediaPlayer GetOrCreateMediaPlayer()
        {
            if (OnboardingVideoPlayer.MediaPlayer != null)
            {
                return OnboardingVideoPlayer.MediaPlayer;
            }

            var mediaPlayer = new MediaPlayer
            {
                IsLoopingEnabled = true,
                IsMuted = true
            };
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            OnboardingVideoPlayer.SetMediaPlayer(mediaPlayer);
            return mediaPlayer;
        }

        private void OnboardingWindow_Closed(object sender, WindowEventArgs args)
        {
            AppWindow.Closing -= AppWindow_Closing;
            Closed -= OnboardingWindow_Closed;

            if (OnboardingVideoPlayer.MediaPlayer == null)
            {
                return;
            }

            OnboardingVideoPlayer.MediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            OnboardingVideoPlayer.MediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
            OnboardingVideoPlayer.MediaPlayer.Pause();
            OnboardingVideoPlayer.MediaPlayer.Dispose();
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (m_allowWindowClose)
            {
                return;
            }

            args.Cancel = true;
            DismissOnboardingWindow();
        }

        private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            if (m_currentPageIndex == 1)
            {
                sender.Play();
            }
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            if (m_hasRetriedCurrentVideoSource)
            {
                return;
            }

            m_hasRetriedCurrentVideoSource = true;

            var isDarkTheme = RootGrid.ActualTheme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                _ => Application.Current?.RequestedTheme == ApplicationTheme.Dark
            };

            var fallbackUri = GetFallbackPackagedVideoUri(isDarkTheme);
            m_currentVideoUri = fallbackUri;
            sender.Source = MediaSource.CreateFromUri(new Uri(fallbackUri));

            if (m_currentPageIndex == 1)
            {
                sender.Play();
            }
        }

        private FrameworkElement GetPage(int pageIndex)
        {
            return pageIndex switch
            {
                0 => PageOneContent,
                1 => PageTwoContent,
                _ => throw new ArgumentOutOfRangeException(nameof(pageIndex))
            };
        }

        private static TranslateTransform GetTranslateTransform(UIElement element)
        {
            if (element.RenderTransform is TranslateTransform translateTransform)
            {
                return translateTransform;
            }

            var createdTransform = new TranslateTransform();
            element.RenderTransform = createdTransform;
            return createdTransform;
        }

    }
}

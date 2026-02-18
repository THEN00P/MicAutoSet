using System;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DontTouchMyMic.Utils;
using H.NotifyIcon;

namespace DontTouchMyMic
{
    public sealed class TrayMenuWindow : Window
    {
        private const int WindowAnimationDurationMs = 120;
        private const int WindowOffsetFromTaskbar = 5;
        private static readonly SizeInt32 TrayMenuSize = new(228, 110);

        private readonly MainWindow m_mainWindow;
        private readonly Brush m_normalBrush;
        private readonly Brush m_pointerOverBrush;
        private readonly Brush m_pressedBrush;

        private readonly TaskbarAnchoredWindowVisibilityController m_visibilityController;
        private readonly WindowAcrylicBackdrop m_acrylicBackdrop;

        public TrayMenuWindow(MainWindow mainWindow)
        {
            m_mainWindow = mainWindow;

            m_normalBrush = ResolveBrush("SubtleFillColorTransparentBrush", Colors.Transparent);
            m_pointerOverBrush = ResolveBrush("SubtleFillColorSecondaryBrush", Color.FromArgb(51, 128, 128, 128));
            m_pressedBrush = ResolveBrush("SubtleFillColorTertiaryBrush", Color.FromArgb(76, 128, 128, 128));

            Content = BuildContent();

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(true, false);
            }

            AppWindow.Resize(WindowScaleHelper.ScaleSizeForWindow(this, TrayMenuSize));
            WindowExtensions.HideInTaskbar(this);

            m_visibilityController = new TaskbarAnchoredWindowVisibilityController(this, WindowOffsetFromTaskbar, WindowAnimationDurationMs);
            m_acrylicBackdrop = new WindowAcrylicBackdrop(this);

            Activated += Window_Activated;
            m_acrylicBackdrop.TryEnable(useAcrylicThin: false);
            Closed += Window_Closed;
        }

        public async Task ToggleWindowAsync()
        {
            if (m_visibilityController.IsVisible)
            {
                await HideWindowAnimatedAsync();
                return;
            }

            await ShowWindowAnimatedAsync();
        }

        private UIElement BuildContent()
        {
            var border = new Border
            {
                Margin = new Thickness(0),
                Padding = new Thickness(2, 2, 2, 2),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(0),
                Background = ResolveBrush("LayerFillColorDefaultBrush", Color.FromArgb(230, 32, 32, 32))
            };

            var stack = new StackPanel { Spacing = 0 };
            stack.Children.Add(CreateMenuButton("Show/Hide Window", "\uE8A0", ShowHideWindow_Click));
            stack.Children.Add(CreateMenuButton("About", "\uE946", OpenAboutWindow_Click));
            stack.Children.Add(new Rectangle
            {
                Margin = new Thickness(0, 2, 0, 2),
                Height = 1,
                Fill = ResolveBrush("DividerStrokeColorDefaultBrush", Color.FromArgb(70, 255, 255, 255))
            });
            stack.Children.Add(CreateMenuButton("Exit", "\uE7E8", ExitApplication_Click));

            border.Child = stack;

            var root = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent)
            };
            root.Children.Add(border);
            return root;
        }

        private Button CreateMenuButton(string text, string glyph, RoutedEventHandler clickHandler)
        {
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            };

            var content = new Grid { ColumnSpacing = 10 };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.Children.Add(icon);
            Grid.SetColumn(label, 1);
            content.Children.Add(label);

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(10, 6, 10, 6),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Background = m_normalBrush,
                Content = content
            };

            button.PointerEntered += MenuButton_PointerEntered;
            button.PointerExited += MenuButton_PointerExited;
            button.PointerPressed += MenuButton_PointerPressed;
            button.PointerReleased += MenuButton_PointerReleased;
            button.Click += clickHandler;

            return button;
        }

        private static Brush ResolveBrush(string resourceKey, Color fallbackColor)
        {
            if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallbackColor);
        }

        private void MenuButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = m_pointerOverBrush;
            }
        }

        private void MenuButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = m_normalBrush;
            }
        }

        private void MenuButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = m_pressedBrush;
            }
        }

        private void MenuButton_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.Background = m_pointerOverBrush;
            }
        }

        private async Task ShowWindowAnimatedAsync()
        {
            await m_visibilityController.ShowAsync(TrayMenuSize);
        }

        private Task<bool> HideWindowAnimatedAsync()
        {
            return m_visibilityController.HideAsync();
        }

        private async void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                await HideWindowAnimatedAsync();
            }
        }

        private async void ShowHideWindow_Click(object sender, RoutedEventArgs e)
        {
            await HideWindowAnimatedAsync();
            await m_mainWindow.OpenWindow();
        }

        private async void OpenAboutWindow_Click(object sender, RoutedEventArgs e)
        {
            await HideWindowAnimatedAsync();
            m_mainWindow.OpenAboutWindow();
        }

        private async void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            await HideWindowAnimatedAsync();
            m_mainWindow.ExitApplication();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            m_visibilityController.Dispose();
            m_acrylicBackdrop.Dispose();
        }
    }
}

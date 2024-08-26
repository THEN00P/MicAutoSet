using System;
using System.Diagnostics;
using System.Threading;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationEventArgs = Microsoft.UI.Xaml.Navigation.NavigationEventArgs;

namespace MicVolumeLock.Pages
{
    public sealed partial class MainPage : Page
    {
        public static readonly SizeInt32 PageSize = new(375, 100);
        
        public MainPage()
        {
            InitializeComponent();

            VolSlider.Value = App.Volume;

            UpdateMuteUi();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (App.MainWindow != null)
            {
                int windowY = App.MainWindow.AppWindow.Position.Y + App.MainWindow.AppWindow.Size.Height -
                              PageSize.Height;
                App.MainWindow.AppWindow.MoveAndResize(new RectInt32(App.MainWindow.AppWindow.Position.X, windowY, PageSize.Width, PageSize.Height));
            }
        }

        private void UpdateMuteUi()
        {
            if (App.Muted)
            {
                MuteIcon.Symbol = Symbol.Mute;
            }
            else
            {
                MuteIcon.Symbol = Symbol.Microphone;
            }
        }
    
        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            App.Mic.AudioEndpointVolume.Mute = !App.Mic.AudioEndpointVolume.Mute;
            App.Muted = App.Mic.AudioEndpointVolume.Mute;

            UpdateMuteUi();
        }

        public void VolSlider_ValueChanged(object sender,
            Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            App.Volume = e.NewValue;
            
            // TODO: this started throwing a low level exception, after a windows sdk update i think
            App.Mic.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(App.Volume / 100);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow.NavigateTo(typeof(SelectorPage));
        }
    }
}

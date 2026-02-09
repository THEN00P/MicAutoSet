using System;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationEventArgs = Microsoft.UI.Xaml.Navigation.NavigationEventArgs;

namespace DontTouchMyMic.Pages
{
    public sealed partial class MainPage : Page
    {
        public static readonly SizeInt32 PageSize = new(375, 100);
        private bool isSyncingUi;
        
        public MainPage()
        {
            InitializeComponent();

            SyncUiFromAppState();

            App.AudioStateChanged += OnAudioStateChanged;
            Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            App.AudioStateChanged -= OnAudioStateChanged;
            Unloaded -= MainPage_Unloaded;
        }

        private void OnAudioStateChanged()
        {
            DispatcherQueue.TryEnqueue(SyncUiFromAppState);
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

        private void SyncUiFromAppState()
        {
            isSyncingUi = true;
            if (Math.Abs(VolSlider.Value - App.Volume) > 0.1)
            {
                VolSlider.Value = App.Volume;
            }
            isSyncingUi = false;

            UpdateMuteUi();
        }
    
        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.Mic == null)
                return;

            App.Muted = !App.Mic.IsMuted;
            App.Mic.SetMuteAsync(App.Muted);

            UpdateMuteUi();
        }

        public void VolSlider_ValueChanged(object sender,
            Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (isSyncingUi)
                return;

            App.SetCurrentMicVolume(e.NewValue);
            App.ApplyCurrentMicVolume();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow.NavigateTo(typeof(SelectorPage));
        }
    }
}

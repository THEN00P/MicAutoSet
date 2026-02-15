using System;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DontTouchMyMic.Pages
{
    public sealed partial class MainPage : Page
    {
        private const string MicOnGlyph = "\uE720";
        private const string MicOffGlyph = "\uEC54";

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

        private void UpdateMuteUi()
        {
            MuteIcon.Glyph = App.Muted ? MicOffGlyph : MicOnGlyph;
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

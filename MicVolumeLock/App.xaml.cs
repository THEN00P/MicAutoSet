using Microsoft.UI.Xaml;
using CoreAudio;
using System;
using System.Diagnostics;
using H.NotifyIcon;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MicVolumeLock
{
    public partial class App : Application
    {
        internal static Guid EnumeratorCtx = Guid.NewGuid();
        internal static MMDevice Mic;
        internal static String MicId;
        internal static double Volume;
        internal static bool Muted;
        internal static MMDeviceEnumerator Enumerator = new(EnumeratorCtx);

        internal static MainWindow MainWindow;

        public App()
        {
            InitializeComponent();

            Mic = Enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            MicId = Mic.ID;
            Volume = Mic.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
            Muted = Mic.AudioEndpointVolume.Mute;

            MMNotificationClient notificationClient = new MMNotificationClient(Enumerator);
            notificationClient.DefaultDeviceChanged += NotificationClientOnDefaultDeviceChanged;

            Mic.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
        }

        private void NotificationClientOnDefaultDeviceChanged(object sender, DefaultDeviceChangedEventArgs e)
        {
            if (e.DeviceId != MicId)
            {
                // We can't use the Global Microphone Objects, because we're running in a different thread
                var localEnumerator = new MMDeviceEnumerator(EnumeratorCtx);
                localEnumerator.GetDevice(MicId).Selected = true;
            }
        }

        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            var uiValueRounded = Math.Round(Volume);
            var micValueRounded = Math.Round(Mic.AudioEndpointVolume.MasterVolumeLevelScalar * 100);

            if (Math.Abs(uiValueRounded - micValueRounded) > 0.1)
            {
                Mic.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(uiValueRounded / 100);
            }

            if (Mic.AudioEndpointVolume.Mute != Muted)
            {
                Mic.AudioEndpointVolume.Mute = Muted;
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();

            MainWindow.Closed += M_window_Closed;
#if !DEBUG
            WindowExtensions.Hide(MainWindow);
#endif
        }

        private void M_window_Closed(object sender, WindowEventArgs args)
        {
            Exit();
        }
    }
}

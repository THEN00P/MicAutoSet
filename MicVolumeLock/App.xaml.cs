using Microsoft.UI.Xaml;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using System;
using AudioSwitcher.AudioApi.Observables;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MicVolumeLock
{
    public partial class App : Application
    {
        internal static CoreAudioController Enumerator;
        internal static Guid EnumeratorCtx = Guid.NewGuid();
        internal static CoreAudioDevice Mic;
        internal static Guid MicId;
        internal static double Volume;
        internal static bool Muted;

        internal static MainWindow MainWindow;

        public App()
        {
            InitializeComponent();

            Enumerator = new();
            Mic = Enumerator.GetDefaultDevice(DeviceType.Capture, Role.Communications);
            MicId = Mic.Id;
            Volume = Mic.Volume;
            Muted = Mic.IsMuted;
            
            Enumerator.AudioDeviceChanged.When(x => 
                x.ChangedType == DeviceChangedType.DefaultChanged && 
                x.Device.Id != MicId
            ).Subscribe(x => Mic.SetAsDefault());
            
            Mic.VolumeChanged.When(x => 
                Math.Abs(x.Volume - Volume) > 0.1
            ).Subscribe(x => Mic.SetVolumeAsync(Volume));

            Mic.MuteChanged.Subscribe(x => Mic.SetMuteAsync(Muted));
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

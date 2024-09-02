using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace MicVolumeLock.Pages
{
    public sealed partial class SelectorPage : Page
    {
        public static readonly SizeInt32 PageSize = new(375, 375);
        private Dictionary<int, CoreAudioDevice> Devices = new();
        
        public SelectorPage()
        {
            InitializeComponent();

            var i = 0;
            foreach (var device in App.Enumerator.GetDevices(DeviceType.Capture, DeviceState.Active))
            {
                Devices.Add(i, device);
                DeviceList.Items.Add(device.FullName);

                if (device.IsDefaultDevice)
                {
                    DeviceList.SelectedIndex = i;
                }
                
                i++;
            }

            DeviceList.SelectionChanged += (sender, args) =>
            {
                App.MicId = Devices[DeviceList.SelectedIndex].Id;
                App.Mic = Devices[DeviceList.SelectedIndex];
                Devices[DeviceList.SelectedIndex].SetAsDefaultAsync();
                Devices[DeviceList.SelectedIndex].SetAsDefaultCommunicationsAsync();
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (App.MainWindow != null)
            {
                int windowY = App.MainWindow.AppWindow.Position.Y + App.MainWindow.AppWindow.Size.Height -
                              PageSize.Height;
                App.MainWindow.AppWindow.Resize(new SizeInt32(PageSize.Width, PageSize.Height));
                App.MainWindow.AppWindow.Move(new PointInt32(App.MainWindow.AppWindow.Position.X, windowY));
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow.NavigateBack();
        }
    }
}

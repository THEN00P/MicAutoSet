using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DontTouchMyMic.Pages
{
    public sealed partial class SelectorPage : Page
    {
        public static readonly SizeInt32 PageSize = new(375, 375);
        public ObservableCollection<CachedMicrophoneListItem> DeviceItems { get; } = new();

        private StartupTask startupTask;

        private async void initStartupTask()
        {
            startupTask = await StartupTask.GetAsync("DontTouchMyMic");

            if(startupTask.State == StartupTaskState.Enabled)
                AutoStartToggle.IsOn = true;
            else
                AutoStartToggle.IsOn = false;

        }

        public SelectorPage()
        {
            InitializeComponent();
            initStartupTask();

            RefreshDeviceList();

            App.CachedMicrophonesChanged += OnCachedMicrophonesChanged;
            Unloaded += SelectorPage_Unloaded;
        }

        private void SelectorPage_Unloaded(object sender, RoutedEventArgs e)
        {
            App.CachedMicrophonesChanged -= OnCachedMicrophonesChanged;
            Unloaded -= SelectorPage_Unloaded;
        }

        private void OnCachedMicrophonesChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshDeviceList);
        }

        private void RefreshDeviceList()
        {
            var cachedMicrophones = App.GetCachedMicrophonesSnapshot();

            DeviceItems.Clear();
            foreach (var cachedMicrophone in cachedMicrophones)
            {
                DeviceItems.Add(new CachedMicrophoneListItem(
                    cachedMicrophone.DeviceId,
                    cachedMicrophone.Name,
                    cachedMicrophone.IsConnected,
                    cachedMicrophone.IsDefault
                ));
            }
        }

        private void DeviceList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (DeviceItems.Count == 0)
                return;

            App.SetCachedMicrophoneOrder(DeviceItems.Select(item => item.DeviceId).ToList());
        }

        private void RemoveCachedDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Guid deviceId)
            {
                App.RemoveCachedMicrophone(deviceId);
            }
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

        private async void ToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Toggled");
            
            var toggleSwitch = sender as ToggleSwitch;

            if(toggleSwitch.IsOn)
                toggleSwitch.IsOn = (await startupTask.RequestEnableAsync() == StartupTaskState.Enabled);
            else
                startupTask.Disable();
        }
    }

    public sealed class CachedMicrophoneListItem
    {
        public CachedMicrophoneListItem(Guid deviceId, string displayName, bool isConnected, bool isDefault)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
            IsConnected = isConnected;
            IsDefault = isDefault;
        }

        public Guid DeviceId { get; }
        public string DisplayName { get; }
        public bool IsConnected { get; }
        public bool IsDefault { get; }
        public bool IsRemoveEnabled => !IsConnected;

        public string StatusText
        {
            get
            {
                if (IsConnected && IsDefault)
                    return "Connected - default";
                if (IsConnected)
                    return "Connected";
                return "Disconnected";
            }
        }

        public double NameOpacity => IsConnected ? 1.0 : 0.55;
        public double StatusOpacity => IsConnected ? 0.9 : 0.55;
    }
}

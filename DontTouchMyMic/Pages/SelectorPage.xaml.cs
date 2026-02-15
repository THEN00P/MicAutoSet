using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DontTouchMyMic.Utils;

namespace DontTouchMyMic.Pages
{
    public sealed partial class SelectorPage : Page
    {
        public static readonly SizeInt32 PageSize = new(375, 375);
        public ObservableCollection<CachedMicrophoneListItem> DeviceItems { get; } = new();

        private bool isUpdatingAutoStartToggle;

        private async void InitAutoStartToggle()
        {
            isUpdatingAutoStartToggle = true;
            try
            {
                AutoStartToggle.IsOn = await AutoStartManager.IsEnabledAsync();
            }
            finally
            {
                isUpdatingAutoStartToggle = false;
            }
        }

        public SelectorPage()
        {
            InitializeComponent();
            InitAutoStartToggle();

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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow.NavigateBack();
        }

        private async void ToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
        {
            if (isUpdatingAutoStartToggle || sender is not ToggleSwitch toggleSwitch)
                return;

            isUpdatingAutoStartToggle = true;
            try
            {
                toggleSwitch.IsOn = await AutoStartManager.SetEnabledAsync(toggleSwitch.IsOn);
            }
            finally
            {
                isUpdatingAutoStartToggle = false;
            }
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

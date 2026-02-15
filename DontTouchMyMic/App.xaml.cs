using Microsoft.UI.Xaml;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AudioSwitcher.AudioApi.Observables;
using DontTouchMyMic.Models;
using H.NotifyIcon;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DontTouchMyMic
{
    public partial class App : Application
    {
        private static readonly object CacheLock = new();
        private static readonly SemaphoreSlim MicSelectionLock = new(1, 1);
        private static readonly SemaphoreSlim VolumeApplyLock = new(1, 1);
        private static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DontTouchMyMic",
            "microphone-cache.json"
        );

        private static readonly List<CachedMicrophoneEntry> CachedMicrophones = new();
        private static readonly Dictionary<Guid, CoreAudioDevice> ConnectedCaptureDevices = new();

        private static IDisposable audioDeviceChangedSubscription;
        private static IDisposable micVolumeSubscription;
        private static IDisposable micMuteSubscription;
        private static int managedVolumeMutationDepth;

        internal static CoreAudioController Enumerator;
        internal static CoreAudioDevice Mic;
        internal static Guid MicId;
        internal static double Volume;
        internal static bool Muted;

        internal static MainWindow MainWindow;
        internal static event Action CachedMicrophonesChanged;
        internal static event Action AudioStateChanged;

        public App()
        {
            InitializeComponent();

            Enumerator = new CoreAudioController();

            LoadCachedMicrophones();
            RefreshConnectedCaptureDevices(includeNewDevices: true);

            Mic = GetHighestPriorityConnectedMicrophone() ??
                  Enumerator.GetDefaultDevice(DeviceType.Capture, Role.Communications);

            if (Mic != null)
            {
                EnsureCachedEntry(Mic, updateVolume: false);
                MicId = Mic.Id;
                Volume = GetCachedVolume(Mic.Id, Mic.Volume);
                Muted = Mic.IsMuted;
                AttachCurrentMicSubscriptions();
            }

            audioDeviceChangedSubscription = Enumerator.AudioDeviceChanged
                .When(x => x.Device != null && x.Device.IsCaptureDevice)
                .Subscribe(x => _ = HandleCaptureDeviceChangeAsync(x));

            _ = EnsurePreferredMicrophoneAsync();
        }

        internal static IReadOnlyList<CachedMicrophoneSnapshot> GetCachedMicrophonesSnapshot()
        {
            RefreshConnectedCaptureDevices(includeNewDevices: false);

            lock (CacheLock)
            {
                return CachedMicrophones
                    .Select(entry =>
                        new CachedMicrophoneSnapshot
                        {
                            DeviceId = entry.DeviceId,
                            Name = entry.Name,
                            IsConnected = ConnectedCaptureDevices.ContainsKey(entry.DeviceId),
                            IsDefault = entry.DeviceId == MicId
                        })
                    .ToList();
            }
        }

        internal static void SetCachedMicrophoneOrder(IReadOnlyList<Guid> orderedDeviceIds)
        {
            if (orderedDeviceIds == null || orderedDeviceIds.Count == 0)
                return;

            bool changed;

            lock (CacheLock)
            {
                var entriesById = CachedMicrophones.ToDictionary(entry => entry.DeviceId, entry => entry);
                var reordered = new List<CachedMicrophoneEntry>(CachedMicrophones.Count);
                var seenIds = new HashSet<Guid>();

                foreach (var deviceId in orderedDeviceIds)
                {
                    if (seenIds.Contains(deviceId))
                        continue;

                    if (entriesById.TryGetValue(deviceId, out var entry))
                    {
                        reordered.Add(entry);
                        seenIds.Add(deviceId);
                    }
                }

                foreach (var entry in CachedMicrophones)
                {
                    if (!seenIds.Contains(entry.DeviceId))
                    {
                        reordered.Add(entry);
                    }
                }

                changed = !CachedMicrophones.Select(entry => entry.DeviceId)
                    .SequenceEqual(reordered.Select(entry => entry.DeviceId));

                if (!changed)
                    return;

                CachedMicrophones.Clear();
                CachedMicrophones.AddRange(reordered);
                SaveCachedMicrophonesNoLock();
            }

            NotifyCachedMicrophonesChanged();
            _ = EnsurePreferredMicrophoneAsync();
        }

        internal static void RemoveCachedMicrophone(Guid deviceId)
        {
            RefreshConnectedCaptureDevices(includeNewDevices: false);

            bool changed;

            lock (CacheLock)
            {
                if (ConnectedCaptureDevices.ContainsKey(deviceId))
                    return;

                changed = CachedMicrophones.RemoveAll(entry => entry.DeviceId == deviceId) > 0;

                if (changed)
                {
                    SaveCachedMicrophonesNoLock();
                }
            }

            if (!changed)
                return;

            NotifyCachedMicrophonesChanged();
            _ = EnsurePreferredMicrophoneAsync();
        }

        internal static void SetCurrentMicVolume(double volume)
        {
            var normalizedVolume = NormalizeVolume(volume);
            Volume = normalizedVolume;

            if (Mic != null)
            {
                SetCachedVolume(Mic.Id, normalizedVolume);
            }

            NotifyAudioStateChanged();
        }

        internal static void ApplyCurrentMicVolume()
        {
            _ = ApplyManagedVolumeToCurrentMicAsync();
        }

        private static async Task HandleCaptureDeviceChangeAsync(DeviceChangedArgs change)
        {
            if (change.Device is not CoreAudioDevice changedMic)
                return;

            if (change.ChangedType == DeviceChangedType.PropertyChanged)
            {
                UpdateCachedName(changedMic.Id, changedMic.FullName);
            }

            RefreshConnectedCaptureDevices(includeNewDevices: true);
            NotifyCachedMicrophonesChanged();

            await EnsurePreferredMicrophoneAsync();
        }

        private static async Task EnsurePreferredMicrophoneAsync()
        {
            await MicSelectionLock.WaitAsync();

            try
            {
                RefreshConnectedCaptureDevices(includeNewDevices: false);

                var preferredMic = GetHighestPriorityConnectedMicrophone();
                if (preferredMic == null)
                    return;

                var cacheChanged = EnsureCachedEntry(preferredMic, updateVolume: false);

                if (Mic == null || Mic.Id != preferredMic.Id)
                {
                    Mic = preferredMic;
                    MicId = preferredMic.Id;
                    AttachCurrentMicSubscriptions();
                }

                var targetVolume = GetCachedVolume(preferredMic.Id, preferredMic.Volume);
                Volume = targetVolume;
                NotifyAudioStateChanged();

                if (!preferredMic.IsDefaultDevice)
                {
                    await preferredMic.SetAsDefaultAsync();
                }

                if (!preferredMic.IsDefaultCommunicationsDevice)
                {
                    await preferredMic.SetAsDefaultCommunicationsAsync();
                }

                await ApplyManagedVolumeToCurrentMicAsync();

                if (preferredMic.IsMuted != Muted)
                {
                    await preferredMic.SetMuteAsync(Muted);
                }

                if (cacheChanged)
                {
                    NotifyCachedMicrophonesChanged();
                }
            }
            catch
            {
                // Ignore device churn errors while endpoints are changing.
            }
            finally
            {
                MicSelectionLock.Release();
            }
        }

        private static void AttachCurrentMicSubscriptions()
        {
            micVolumeSubscription?.Dispose();
            micMuteSubscription?.Dispose();

            if (Mic == null)
                return;

            var currentMic = Mic;

            micVolumeSubscription = currentMic.VolumeChanged
                .When(x => Math.Abs(x.Volume - Volume) > 0.1)
                .Subscribe(x =>
                {
                    if (IsManagedVolumeMutationInProgress() || Mic == null || currentMic.Id != Mic.Id)
                        return;

                    _ = ApplyManagedVolumeToCurrentMicAsync();
                });

            micMuteSubscription = currentMic.MuteChanged
                .When(x => x.IsMuted != Muted)
                .Subscribe(x => _ = currentMic.SetMuteAsync(Muted));
        }

        private static bool EnsureCachedEntry(CoreAudioDevice device, bool updateVolume)
        {
            var changed = false;

            lock (CacheLock)
            {
                var existing = CachedMicrophones.FirstOrDefault(entry => entry.DeviceId == device.Id);

                if (existing == null)
                {
                    CachedMicrophones.Add(new CachedMicrophoneEntry
                    {
                        DeviceId = device.Id,
                        Name = device.FullName,
                        Volume = NormalizeVolume(device.Volume)
                    });

                    SaveCachedMicrophonesNoLock();
                    return true;
                }

                if (!string.Equals(existing.Name, device.FullName, StringComparison.Ordinal))
                {
                    existing.Name = device.FullName;
                    changed = true;
                }

                if (updateVolume && device.Volume >= 0 && Math.Abs(existing.Volume - device.Volume) > 0.1)
                {
                    existing.Volume = NormalizeVolume(device.Volume);
                    changed = true;
                }

                if (changed)
                {
                    SaveCachedMicrophonesNoLock();
                }
            }

            return changed;
        }

        private static bool UpdateCachedName(Guid deviceId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (CacheLock)
            {
                var existing = CachedMicrophones.FirstOrDefault(entry => entry.DeviceId == deviceId);
                if (existing == null)
                    return false;

                if (string.Equals(existing.Name, name, StringComparison.Ordinal))
                    return false;

                existing.Name = name;
                SaveCachedMicrophonesNoLock();
                return true;
            }
        }

        private static void RefreshConnectedCaptureDevices(bool includeNewDevices)
        {
            var captureDevices = Enumerator
                .GetDevices(DeviceType.Capture, DeviceState.All)
                .ToList();
            var windowsDefaultCaptureId = GetWindowsDefaultCaptureDeviceId();

            bool cacheChanged = false;

            lock (CacheLock)
            {
                ConnectedCaptureDevices.Clear();

                bool isInitialCacheSeed = includeNewDevices && CachedMicrophones.Count == 0;

                foreach (var captureDevice in captureDevices)
                {
                    if (captureDevice.State == DeviceState.Active)
                    {
                        ConnectedCaptureDevices[captureDevice.Id] = captureDevice;
                    }
                }

                if (isInitialCacheSeed && windowsDefaultCaptureId.HasValue &&
                    ConnectedCaptureDevices.TryGetValue(windowsDefaultCaptureId.Value, out var windowsDefaultCaptureDevice) &&
                    CachedMicrophones.All(entry => entry.DeviceId != windowsDefaultCaptureDevice.Id))
                {
                    CachedMicrophones.Add(new CachedMicrophoneEntry
                    {
                        DeviceId = windowsDefaultCaptureDevice.Id,
                        Name = windowsDefaultCaptureDevice.FullName,
                        Volume = NormalizeVolume(windowsDefaultCaptureDevice.Volume)
                    });
                    cacheChanged = true;
                }

                if (includeNewDevices)
                {
                    foreach (var captureDevice in captureDevices)
                    {
                        if (captureDevice.State != DeviceState.Active)
                            continue;

                        if (CachedMicrophones.All(entry => entry.DeviceId != captureDevice.Id))
                        {
                            CachedMicrophones.Add(new CachedMicrophoneEntry
                            {
                                DeviceId = captureDevice.Id,
                                Name = captureDevice.FullName,
                                Volume = NormalizeVolume(captureDevice.Volume)
                            });
                            cacheChanged = true;
                        }
                    }
                }

                if (cacheChanged)
                {
                    SaveCachedMicrophonesNoLock();
                }
            }

            if (cacheChanged)
            {
                NotifyCachedMicrophonesChanged();
            }
        }

        private static CoreAudioDevice GetHighestPriorityConnectedMicrophone()
        {
            lock (CacheLock)
            {
                foreach (var cachedMicrophone in CachedMicrophones)
                {
                    if (ConnectedCaptureDevices.TryGetValue(cachedMicrophone.DeviceId, out var connectedDevice))
                    {
                        return connectedDevice;
                    }
                }

                return ConnectedCaptureDevices.Values.FirstOrDefault();
            }
        }

        private static Guid? GetWindowsDefaultCaptureDeviceId()
        {
            try
            {
                var defaultCaptureDevice = Enumerator.GetDefaultDevice(DeviceType.Capture, Role.Communications)
                    ?? Enumerator.GetDefaultDevice(DeviceType.Capture, Role.Console)
                    ?? Enumerator.GetDefaultDevice(DeviceType.Capture, Role.Multimedia);

                return defaultCaptureDevice?.Id;
            }
            catch
            {
                return null;
            }
        }

        private static double GetCachedVolume(Guid deviceId, double fallbackVolume)
        {
            bool added = false;
            double result;

            lock (CacheLock)
            {
                var existing = CachedMicrophones.FirstOrDefault(entry => entry.DeviceId == deviceId);

                if (existing == null)
                {
                    existing = new CachedMicrophoneEntry
                    {
                        DeviceId = deviceId,
                        Name = ConnectedCaptureDevices.TryGetValue(deviceId, out var connectedDevice)
                            ? connectedDevice.FullName
                            : "Unknown microphone",
                        Volume = NormalizeVolume(fallbackVolume)
                    };

                    CachedMicrophones.Add(existing);
                    SaveCachedMicrophonesNoLock();
                    added = true;
                }

                result = NormalizeVolume(existing.Volume);
            }

            if (added)
            {
                NotifyCachedMicrophonesChanged();
            }

            return result;
        }

        private static void SetCachedVolume(Guid deviceId, double volume)
        {
            lock (CacheLock)
            {
                var existing = CachedMicrophones.FirstOrDefault(entry => entry.DeviceId == deviceId);
                if (existing == null)
                    return;

                if (Math.Abs(existing.Volume - volume) <= 0.1)
                    return;

                existing.Volume = NormalizeVolume(volume);
                SaveCachedMicrophonesNoLock();
            }
        }

        private static bool IsManagedVolumeMutationInProgress()
        {
            return Volatile.Read(ref managedVolumeMutationDepth) > 0;
        }

        private static async Task ApplyManagedVolumeToCurrentMicAsync()
        {
            await VolumeApplyLock.WaitAsync();
            Interlocked.Increment(ref managedVolumeMutationDepth);

            try
            {
                var currentMic = Mic;
                if (currentMic == null)
                    return;

                var targetVolume = Volume;
                if (Math.Abs(currentMic.Volume - targetVolume) <= 0.1)
                    return;

                await currentMic.SetVolumeAsync(targetVolume);
            }
            catch
            {
                // Ignore transient volume write failures during device churn.
            }
            finally
            {
                Interlocked.Decrement(ref managedVolumeMutationDepth);
                VolumeApplyLock.Release();
            }
        }

        private static double NormalizeVolume(double volume)
        {
            if (double.IsNaN(volume) || volume < 0)
                return 0;

            if (volume > 100)
                return 100;

            return volume;
        }

        private static void LoadCachedMicrophones()
        {
            lock (CacheLock)
            {
                CachedMicrophones.Clear();

                if (!File.Exists(CacheFilePath))
                    return;

                try
                {
                    var json = File.ReadAllText(CacheFilePath);
                    var loadedEntries = JsonSerializer.Deserialize<List<CachedMicrophoneEntry>>(json);

                    if (loadedEntries == null)
                        return;

                    var deduplicated = new HashSet<Guid>();

                    foreach (var entry in loadedEntries)
                    {
                        if (entry == null || entry.DeviceId == Guid.Empty || deduplicated.Contains(entry.DeviceId))
                            continue;

                        entry.Name = string.IsNullOrWhiteSpace(entry.Name) ? "Unknown microphone" : entry.Name;
                        entry.Volume = NormalizeVolume(entry.Volume);

                        CachedMicrophones.Add(entry);
                        deduplicated.Add(entry.DeviceId);
                    }
                }
                catch
                {
                    CachedMicrophones.Clear();
                }
            }
        }

        private static void SaveCachedMicrophonesNoLock()
        {
            var directory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(CachedMicrophones);
            File.WriteAllText(CacheFilePath, json);
        }

        private static void NotifyCachedMicrophonesChanged()
        {
            CachedMicrophonesChanged?.Invoke();
        }

        private static void NotifyAudioStateChanged()
        {
            AudioStateChanged?.Invoke();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.HideInTaskbar();
            MainWindow.Activate();

            MainWindow.Closed += M_window_Closed;
#if !DEBUG
            WindowExtensions.Hide(MainWindow);
#endif
        }

        private void M_window_Closed(object sender, WindowEventArgs args)
        {
            audioDeviceChangedSubscription?.Dispose();
            micVolumeSubscription?.Dispose();
            micMuteSubscription?.Dispose();
            Exit();
        }
    }
}

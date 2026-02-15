using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace DontTouchMyMic.Utils
{
    internal static class AutoStartManager
    {
        private const string StartupTaskId = "DontTouchMyMic";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "DontTouchMyMic";

        internal static async Task<bool> IsEnabledAsync()
        {
            if (IsPackagedApp())
            {
                var startupTask = await StartupTask.GetAsync(StartupTaskId);
                return startupTask.State == StartupTaskState.Enabled || startupTask.State == StartupTaskState.EnabledByPolicy;
            }

            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = runKey?.GetValue(RunValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }

        internal static async Task<bool> SetEnabledAsync(bool enabled)
        {
            if (IsPackagedApp())
            {
                var startupTask = await StartupTask.GetAsync(StartupTaskId);
                if (enabled)
                {
                    var state = await startupTask.RequestEnableAsync();
                    return state == StartupTaskState.Enabled || state == StartupTaskState.EnabledByPolicy;
                }

                startupTask.Disable();
                return false;
            }

            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (runKey == null)
                return false;

            if (enabled)
            {
                var executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executablePath))
                    return false;

                runKey.SetValue(RunValueName, $"\"{executablePath}\"");
                return true;
            }

            runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
            return false;
        }

        private static bool IsPackagedApp()
        {
            try
            {
                _ = Package.Current;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

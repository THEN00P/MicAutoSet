using System;
using Microsoft.Win32;

namespace DontTouchMyMic.Utils
{
    internal static class OnboardingStateManager
    {
        private const string AppRegistryPath = @"Software\THEN00P\DontTouchMyMic";
        private const string ShowOnboardingValueName = "ShowOnboarding";

        internal static bool ShouldShowOnLaunch()
        {
            try
            {
                using var appKey = Registry.CurrentUser.OpenSubKey(AppRegistryPath, writable: false);
                var value = appKey?.GetValue(ShowOnboardingValueName);
                return IsEnabledValue(value);
            }
            catch
            {
                return false;
            }
        }

        internal static void MarkShown()
        {
            try
            {
                using var appKey = Registry.CurrentUser.CreateSubKey(AppRegistryPath, writable: true);
                appKey?.DeleteValue(ShowOnboardingValueName, throwOnMissingValue: false);
            }
            catch
            {
                // Ignore registry write failures. Onboarding will only repeat if we cannot clear the marker.
            }
        }

        private static bool IsEnabledValue(object value)
        {
            return value switch
            {
                int intValue => intValue != 0,
                long longValue => longValue != 0,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed != 0,
                _ => false
            };
        }
    }
}

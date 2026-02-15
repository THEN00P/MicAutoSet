using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace DontTouchMyMic.Utils
{
    internal static class AppMetadata
    {
        private const string AppDisplayName = "Dont touch my mic";

        internal static string GetInstalledVersion()
        {
            try
            {
                var version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                var uninstallVersion = GetUninstallDisplayVersion();
                if (!string.IsNullOrWhiteSpace(uninstallVersion))
                    return uninstallVersion;

                var processPath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
                {
                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(processPath);
                    if (!string.IsNullOrWhiteSpace(fileVersionInfo.ProductVersion))
                        return fileVersionInfo.ProductVersion;

                    if (!string.IsNullOrWhiteSpace(fileVersionInfo.FileVersion))
                        return fileVersionInfo.FileVersion;
                }

                return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
            }
        }

        internal static string GetBuildDate()
        {
            var buildMachineTime = NormalizeBuildValue(GetAssemblyMetadataValue("BuildMachineTime"));
            if (buildMachineTime != null)
                return buildMachineTime;

            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
                return "Unknown";

            return File.GetLastWriteTimeUtc(processPath).ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        }

        internal static string GetBuildBranch()
        {
            var buildBranch = NormalizeBuildValue(GetAssemblyMetadataValue("BuildBranch"));
            if (buildBranch != null)
                return buildBranch;

            var gitHeadInfo = TryReadGitHeadInfo();
            if (!string.IsNullOrWhiteSpace(gitHeadInfo.Branch))
                return gitHeadInfo.Branch;

            return "unknown";
        }

        internal static string GetBuildCommit()
        {
            var commit = NormalizeBuildValue(GetAssemblyMetadataValue("BuildCommit"));
            if (commit != null)
                return ShortenCommit(commit);

            var informationalVersionCommit = TryGetCommitFromInformationalVersion();
            if (informationalVersionCommit != null)
                return ShortenCommit(informationalVersionCommit);

            var gitHeadInfo = TryReadGitHeadInfo();
            if (!string.IsNullOrWhiteSpace(gitHeadInfo.Commit))
                return ShortenCommit(gitHeadInfo.Commit);

            return "unknown";
        }

        private static string NormalizeBuildValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase) ? null : value;
        }

        private static string TryGetCommitFromInformationalVersion()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
                return null;

            var informationalVersion = entryAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(informationalVersion))
                return null;

            var separatorIndex = informationalVersion.IndexOf('+');
            if (separatorIndex < 0 || separatorIndex + 1 >= informationalVersion.Length)
                return null;

            return informationalVersion[(separatorIndex + 1)..].Trim();
        }

        private static string GetAssemblyMetadataValue(string key)
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
                return null;

            return entryAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
                ?.Value;
        }

        private static (string Branch, string Commit) TryReadGitHeadInfo()
        {
            try
            {
                var repoRoot = FindRepositoryRoot();
                if (repoRoot == null)
                    return (null, null);

                var gitDirectory = Path.Combine(repoRoot, ".git");
                var headFilePath = Path.Combine(gitDirectory, "HEAD");
                if (!File.Exists(headFilePath))
                    return (null, null);

                var headContent = File.ReadAllText(headFilePath).Trim();
                if (string.IsNullOrWhiteSpace(headContent))
                    return (null, null);

                if (!headContent.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
                    return (null, headContent);

                var refPath = headContent[4..].Trim();
                var branch = Path.GetFileName(refPath.Replace('/', '\\'));
                var commit = ReadCommitFromRef(gitDirectory, refPath);

                return (branch, commit);
            }
            catch
            {
                return (null, null);
            }
        }

        private static string ReadCommitFromRef(string gitDirectory, string refPath)
        {
            var refFilePath = Path.Combine(gitDirectory, refPath.Replace('/', '\\'));
            if (File.Exists(refFilePath))
            {
                return File.ReadAllText(refFilePath).Trim();
            }

            var packedRefsPath = Path.Combine(gitDirectory, "packed-refs");
            if (!File.Exists(packedRefsPath))
                return null;

            foreach (var line in File.ReadLines(packedRefsPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line[0] == '#' || line[0] == '^')
                    continue;

                var separatorIndex = line.IndexOf(' ');
                if (separatorIndex <= 0)
                    continue;

                var candidateRef = line[(separatorIndex + 1)..].Trim();
                if (!string.Equals(candidateRef, refPath, StringComparison.Ordinal))
                    continue;

                return line[..separatorIndex].Trim();
            }

            return null;
        }

        private static string FindRepositoryRoot()
        {
            var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            while (currentDirectory != null)
            {
                var gitPath = Path.Combine(currentDirectory.FullName, ".git");
                if (Directory.Exists(gitPath))
                    return currentDirectory.FullName;

                currentDirectory = currentDirectory.Parent;
            }

            return null;
        }

        private static string ShortenCommit(string commit)
        {
            return commit.Length > 12 ? commit[..12] : commit;
        }

        private static string GetUninstallDisplayVersion()
        {
            return GetUninstallDisplayVersionFromHive(RegistryHive.LocalMachine, RegistryView.Registry64)
                ?? GetUninstallDisplayVersionFromHive(RegistryHive.LocalMachine, RegistryView.Registry32)
                ?? GetUninstallDisplayVersionFromHive(RegistryHive.CurrentUser, RegistryView.Default);
        }

        private static string GetUninstallDisplayVersionFromHive(RegistryHive hive, RegistryView view)
        {
            const string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = baseKey.OpenSubKey(uninstallPath, writable: false);
                if (uninstallKey == null)
                    return null;

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var appKey = uninstallKey.OpenSubKey(subKeyName, writable: false);
                    if (appKey == null)
                        continue;

                    var displayName = appKey.GetValue("DisplayName") as string;
                    if (!string.Equals(displayName, AppDisplayName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var displayVersion = appKey.GetValue("DisplayVersion") as string;
                    if (!string.IsNullOrWhiteSpace(displayVersion))
                        return displayVersion;
                }
            }
            catch
            {
            }

            return null;
        }
    }
}

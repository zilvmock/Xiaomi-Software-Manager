using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using XiaomiSoftwareManager.Models;

namespace XiaomiSoftwareManager
{
    internal class Updater
    {        
        public event Action<string>? DownloadSpeedChanged;
        public event Action<string>? DownloadSizeChanged;
        public event Action<int>? DownloadPercentChanged;
        public event Action? DownloadStarted;

        private readonly string repoUrl = "https://api.github.com/repos/zilvmock/Xiaomi-Software-Manager/releases";
        private readonly string downloadFolder = Directory.GetCurrentDirectory();
        private readonly string headerName = "XiaomiSoftwareManager";

        private DateTime startTime;


        public async Task<GitHubRelease?> GetLatestReleaseAsync(bool checkForLatestPreRelease = false)
        {
            return await GetReleaseAsync(checkForLatestPreRelease);
        }

        private async Task<GitHubRelease?> GetReleaseAsync(bool checkForLatestPreRelease = false)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", headerName);

            try
            {
                HttpResponseMessage response = await client.GetAsync(repoUrl);
                response.EnsureSuccessStatusCode(); // Throws an exception if the status code is not 2xx
                string json = await response.Content.ReadAsStringAsync();
                List<GitHubRelease> releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(json) ??
                    throw new InvalidOperationException("Failed to deserialize the release or the release data is missing.");
                GitHubRelease? release = null;
                
                if (checkForLatestPreRelease)
                {
                    release = releases.OrderByDescending(r => r.CreatedAt).Where(r => r.Prerelease).FirstOrDefault();
                }
                else
                {
                    release = releases.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
                }

                return release;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool updateIsAvailable(string currentVersion, string latestVersion)
        {
            bool IsValidVersionFormat(string version)
            {
                string pattern = @"^v\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$";
                return Regex.IsMatch(version, pattern);
            }

            if (!IsValidVersionFormat(currentVersion) || !IsValidVersionFormat(latestVersion)) { return false; }

            (int major, int minor, int patch, string prerelease) ParseVersion(string version)
            {
                version = version.TrimStart('v');
                string[] parts = version.Split('-');
                string[] numbers = parts[0].Split('.');

                int major = int.Parse(numbers[0]);
                int minor = int.Parse(numbers[1]);
                int patch = int.Parse(numbers[2]);
                string prerelease = parts.Length > 1 ? parts[1] : null;

                return (major, minor, patch, prerelease);
            }

            var (currentMajor, currentMinor, currentPatch, currentPrerelease) = ParseVersion(currentVersion);
            var (latestMajor, latestMinor, latestPatch, latestPrerelease) = ParseVersion(latestVersion);

            // Compare major, minor, patch
            if (latestMajor > currentMajor) return true;
            if (latestMajor < currentMajor) return false;

            if (latestMinor > currentMinor) return true;
            if (latestMinor < currentMinor) return false;

            if (latestPatch > currentPatch) return true;
            if (latestPatch < currentPatch) return false;

            // Compare prerelease (null means stable)
            if (currentPrerelease == null && latestPrerelease != null) return false;    // Current is stable, latest is prerelease
            if (currentPrerelease != null && latestPrerelease == null) return true;     // Latest is stable, current is prerelease

            // If both are prereleases, compare lexicographically
            if (currentPrerelease != null && latestPrerelease != null)
            {
                return string.Compare(latestPrerelease, currentPrerelease, StringComparison.Ordinal) > 0;
            }

            return false;
        }

        private void UpdateDownloadPercent(int progress)
        {
            DownloadPercentChanged?.Invoke(progress);
        }

        private void UpdateDownloadSpeed(string status)
        {
            DownloadSpeedChanged?.Invoke(status);
        }

        private void UpdateDownloadSize(string status)
        {
            DownloadSizeChanged?.Invoke(status);
        }

        public async Task<string> DownloadUpdateAsync(GitHubRelease release)
        {
            try
            {
                GitHubAsset zipAsset = release.Assets.First(x => x.ContentType == "application/zip");
                string fileName = Path.Combine(downloadFolder, zipAsset.Name);

                using (HttpClient client = new())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", headerName);

                    using (var response = await client.GetAsync(zipAsset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        long totalBytes = zipAsset.Size;
                        long downloadedBytes = 0;
                        byte[] buffer = new byte[65536]; // 64 KB
                        int bytesRead;
                        startTime = DateTime.MinValue;

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            startTime = DateTime.Now;
                            DownloadStarted?.Invoke();
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);

                                downloadedBytes += bytesRead;

                                // Update download progress
                                int progress = (int)((double)downloadedBytes / totalBytes * 100);
                                UpdateDownloadSpeed($"{FormatSpeed(totalBytes, downloadedBytes, progress)}");
                                UpdateDownloadSize($"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}");
                                UpdateDownloadPercent(progress);
                            }
                        }
                    }

                    UpdateDownloadSpeed("-");
                    return fileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while downloading or updating: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            else if (bytes < 1048576) return $"{bytes / 1024.0:F2} KB";
            else return $"{bytes / 1048576.0:F2} MB";
        }

        private string FormatBytesPerSecond(double speed)
        {
            if (speed < 1024)
                return $"{speed:F2} B/s";
            else if (speed < 1048576)
                return $"{speed / 1024.0:F2} KB/s";
            else if (speed < 1073741824)
                return $"{speed / 1048576.0:F2} MB/s";
            else
                return $"{speed / 1073741824.0:F2} GB/s";
        }

        private string FormatSpeed(long totalBytes, long downloadedBytes, double progress)
        { 
            double elapsedTime = (DateTime.Now - startTime).TotalSeconds;
            double speed = downloadedBytes / elapsedTime;
            return FormatBytesPerSecond(speed);
        }

        public void InstallUpdate(string zipFilePath)
        {
            try
            {
                string updaterPath = Path.Combine(downloadFolder, "Updater.exe");

                if (!File.Exists(updaterPath))
                    throw new FileNotFoundException("Updater application is missing.");

                int processId = Environment.ProcessId;
                string processName = Process.GetCurrentProcess().ProcessName;
                string executablePath = Environment.ProcessPath!;

                string arguments = $"\"{processId}\" \"{processName}\" \"{executablePath}\" \"{zipFilePath}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    UseShellExecute = true,
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while attempting to update: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

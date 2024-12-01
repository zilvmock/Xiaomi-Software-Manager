using Newtonsoft.Json;
using System.Net.Http;
using System.Windows;
using XiaomiSoftwareManager.Models;

namespace XiaomiSoftwareManager
{
    internal class Updater
    {
        public string UpdateUrl { get; set; } = string.Empty;

        private readonly string repoUrl = "https://api.github.com/repos/zilvmock/Xiaomi-Software-Manager/releases";

        public async Task<GitHubRelease?> GetLatestReleaseAsync(bool checkForLatestPreRelease = false)
        {
            return await GetReleaseAsync(checkForLatestPreRelease);
        }

        private async Task<GitHubRelease?> GetReleaseAsync(bool checkForLatestPreRelease = false)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "XiaomiSoftwareManager");

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
            // Helper to validate the version format
            bool IsValidVersionFormat(string version)
            {
                string pattern = @"^v\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$";
                return System.Text.RegularExpressions.Regex.IsMatch(version, pattern);
            }

            if (!IsValidVersionFormat(currentVersion) || !IsValidVersionFormat(latestVersion)) { return false; }

            // Helper to parse version and prerelease
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

        public void DownloadUpdate(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}

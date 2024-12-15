using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using XiaomiSoftwareManager.Models;

namespace XiaomiSoftwareManager.DownloadManager
{
	public class Updater
	{
		public event Action<string>? DownloadSpeedChanged;
		public event Action<string>? DownloadSizeChanged;
		public event Action<int>? DownloadPercentChanged;
		public event Action? DownloadStarted;

		private readonly string repoUrl = "https://api.github.com/repos/zilvmock/Xiaomi-Software-Manager/releases";
		private readonly string downloadFolder = Directory.GetCurrentDirectory();
		private readonly string headerName = "XiaomiSoftwareManager";

		public async Task<GitHubRelease?> GetLatestReleaseAsync(HttpClient httpClient, bool checkForLatestPreRelease = false)
		{
			return await GetReleaseAsync(httpClient, checkForLatestPreRelease);
		}

		private async Task<GitHubRelease?> GetReleaseAsync(HttpClient client = null!, bool checkForLatestPreRelease = false)
		{
			client ??= new HttpClient();
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

		public static bool UpdateIsAvailable(string currentVersion, string latestVersion)
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

				int major = int.Parse(numbers[0])!;
				int minor = int.Parse(numbers[1])!;
				int patch = int.Parse(numbers[2])!;
				string prerelease = parts.Length > 1 ? parts[1] : null!;

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

		public async Task<string> DownloadUpdateAsync(GitHubRelease release, HttpClient client = null!, string test = "default")
		{
			try
			{
				GitHubAsset zipAsset = release.Assets.First(x => x.ContentType == "application/zip");
				client ??= new HttpClient();
				client.DefaultRequestHeaders.Add("User-Agent", headerName);

				DownloadManager downloadManager = new();
				downloadManager.DownloadSpeedChanged += DownloadSpeedChanged;
				downloadManager.DownloadSizeChanged += DownloadSizeChanged;
				downloadManager.DownloadPercentChanged += DownloadPercentChanged;
				downloadManager.DownloadStarted += DownloadStarted;
				return await downloadManager.DownloadFileAsync(
					zipAsset.BrowserDownloadUrl,
					client: client,
					fileName: zipAsset.Name,
					downloadPath: downloadFolder);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"An error occurred while downloading the update: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return string.Empty;
			}
		}

		public void InstallUpdate(string zipFilePath)
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
	}
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace xsm.Logic.Updates;

public sealed class UpdateManager
{
	private const string RepoOwner = "zilvmock";
	private const string RepoName = "Xiaomi-Software-Manager";
	private const string UserAgent = "XSM";
	private static readonly Uri ReleasesEndpoint = new($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases");
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public const string UpdaterExeName = "xsm.updater.exe";
	public const string DefaultAssetPrefix = "xsm-win-x64-";

	private readonly HttpClient _httpClient;
	private readonly string _assetPrefix;

	public UpdateManager(HttpClient? httpClient = null, string? assetPrefix = null)
	{
		_httpClient = httpClient ?? new HttpClient();
		_assetPrefix = string.IsNullOrWhiteSpace(assetPrefix) ? DefaultAssetPrefix : assetPrefix;
		if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
		{
			_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
		}

		if (!_httpClient.DefaultRequestHeaders.Accept.Any(header => header.MediaType == "application/vnd.github+json"))
		{
			_httpClient.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		}
	}

	public async Task<UpdateCheckResult> CheckForUpdatesAsync(
		string currentVersion,
		bool includePrerelease,
		CancellationToken cancellationToken = default)
	{
		if (!SemanticVersion.TryParse(currentVersion, out var current))
		{
			return UpdateCheckResult.Failed($"Current version '{currentVersion}' cannot be parsed.");
		}

		var releases = await GetReleasesAsync(cancellationToken);
		var latestStable = GetLatestRelease(releases, prerelease: false);
		var latestPreRelease = GetLatestRelease(releases, prerelease: true);

		var selected = includePrerelease
			? SelectPreferredRelease(latestStable, latestPreRelease)
			: latestStable;

		if (selected == null)
		{
			return UpdateCheckResult.Failed("No releases are available yet.");
		}

		if (current.CompareTo(selected.Version) >= 0)
		{
			return UpdateCheckResult.UpToDate("You're already on the latest version.");
		}

		var asset = SelectAsset(selected.Release, selected.Version);
		if (asset == null)
		{
			return UpdateCheckResult.Failed("Update package is missing from the release.");
		}

		return UpdateCheckResult.Available(selected.Version.ToString(), selected.Release, asset);
	}

	public async Task<string?> DownloadAssetAsync(
		GitHubAsset asset,
		string destinationDirectory,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
		{
			return null;
		}

		Directory.CreateDirectory(destinationDirectory);
		var targetPath = Path.Combine(destinationDirectory, asset.Name);

		using var response = await _httpClient.GetAsync(
			asset.BrowserDownloadUrl,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
		await using var fileStream = new FileStream(
			targetPath,
			FileMode.Create,
			FileAccess.Write,
			FileShare.None);
		await contentStream.CopyToAsync(fileStream, cancellationToken);

		return targetPath;
	}

	public bool TryLaunchUpdater(
		string updaterPath,
		int processId,
		string executablePath,
		string zipPath,
		out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(executablePath))
		{
			error = "Executable path is unavailable.";
			return false;
		}

		try
		{
			var arguments = $"{processId} \"{executablePath}\" \"{zipPath}\"";
			Process.Start(new ProcessStartInfo
			{
				FileName = updaterPath,
				Arguments = arguments,
				UseShellExecute = true
			});

			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private async Task<List<GitHubRelease>> GetReleasesAsync(CancellationToken cancellationToken)
	{
		using var response = await _httpClient.GetAsync(ReleasesEndpoint, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken);
		return releases ?? new List<GitHubRelease>();
	}

	private static ReleaseCandidate? GetLatestRelease(IEnumerable<GitHubRelease> releases, bool prerelease)
	{
		ReleaseCandidate? latest = null;
		foreach (var release in releases.Where(release => release.Prerelease == prerelease && !release.Draft))
		{
			if (!SemanticVersion.TryParse(release.TagName, out var version))
			{
				continue;
			}

			if (latest == null || version.CompareTo(latest.Version) > 0)
			{
				latest = new ReleaseCandidate(release, version);
			}
		}

		return latest;
	}

	private static ReleaseCandidate? SelectPreferredRelease(ReleaseCandidate? stable, ReleaseCandidate? prerelease)
	{
		if (stable == null)
		{
			return prerelease;
		}

		if (prerelease == null)
		{
			return stable;
		}

		return prerelease.Version.CompareTo(stable.Version) > 0 ? prerelease : stable;
	}

	private GitHubAsset? SelectAsset(GitHubRelease release, SemanticVersion version)
	{
		var expectedName = $"{_assetPrefix}{version}.zip";

		var exact = release.Assets.FirstOrDefault(asset =>
			string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase));
		if (exact != null)
		{
			return exact;
		}

		var winAsset = release.Assets.FirstOrDefault(asset =>
			asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
			asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase));
		if (winAsset != null)
		{
			return winAsset;
		}

		return release.Assets.FirstOrDefault(asset =>
			asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
	}

	private sealed record ReleaseCandidate(GitHubRelease Release, SemanticVersion Version);
}

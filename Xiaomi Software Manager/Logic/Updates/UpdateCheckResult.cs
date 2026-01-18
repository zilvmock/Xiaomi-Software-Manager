namespace xsm.Logic.Updates;

public sealed class UpdateCheckResult
{
	private UpdateCheckResult(
		bool isUpdateAvailable,
		string statusMessage,
		string? latestVersion,
		GitHubRelease? release,
		GitHubAsset? asset)
	{
		IsUpdateAvailable = isUpdateAvailable;
		StatusMessage = statusMessage;
		LatestVersion = latestVersion;
		Release = release;
		Asset = asset;
	}

	public bool IsUpdateAvailable { get; }
	public string StatusMessage { get; }
	public string? LatestVersion { get; }
	public GitHubRelease? Release { get; }
	public GitHubAsset? Asset { get; }

	public static UpdateCheckResult UpToDate(string message)
		=> new(false, message, null, null, null);

	public static UpdateCheckResult Available(string latestVersion, GitHubRelease release, GitHubAsset asset)
		=> new(true, $"Update {latestVersion} is available.", latestVersion, release, asset);

	public static UpdateCheckResult Failed(string message)
		=> new(false, message, null, null, null);
}

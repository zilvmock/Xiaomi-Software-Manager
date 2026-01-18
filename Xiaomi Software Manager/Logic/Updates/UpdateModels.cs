using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace xsm.Logic.Updates;

public sealed class GitHubRelease
{
	[JsonPropertyName("tag_name")]
	public string TagName { get; set; } = string.Empty;

	[JsonPropertyName("prerelease")]
	public bool Prerelease { get; set; }

	[JsonPropertyName("draft")]
	public bool Draft { get; set; }

	[JsonPropertyName("assets")]
	public List<GitHubAsset> Assets { get; set; } = new();
}

public sealed class GitHubAsset
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("browser_download_url")]
	public string BrowserDownloadUrl { get; set; } = string.Empty;

	[JsonPropertyName("content_type")]
	public string ContentType { get; set; } = string.Empty;
}

using System;
using System.IO;
using xsm.Logic.Helpers;

namespace xsm.Logic.Downloads
{
	public static class DownloadUrlBuilder
	{
		public static bool TryGetFileName(string? webLink, out string fileName, out string? error)
		{
			fileName = string.Empty;
			error = null;

			if (string.IsNullOrWhiteSpace(webLink))
			{
				error = "Web link is missing.";
				return false;
			}

			if (!Uri.TryCreate(webLink, UriKind.Absolute, out var uri))
			{
				error = "Web link is invalid.";
				return false;
			}

			fileName = Path.GetFileName(uri.LocalPath);
			if (string.IsNullOrWhiteSpace(fileName))
			{
				error = "File name not found in web link.";
				return false;
			}

			if (!fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
			{
				fileName += ".tgz";
			}

			return true;
		}

		public static bool TryGetVersionString(string? webVersion, string? webLink, out string version, out string? error)
		{
			version = string.Empty;
			error = null;

			if (!string.IsNullOrWhiteSpace(webVersion))
			{
				version = webVersion;
				return true;
			}

			if (string.IsNullOrWhiteSpace(webLink))
			{
				error = "Web link is missing.";
				return false;
			}

			if (SoftwareVersion.TryParse(webLink, out var parsed))
			{
				version = parsed.Raw;
				return true;
			}

			error = "Version string not found.";
			return false;
		}

		public static bool TryBuildDownloadUri(
			string? webLink,
			string? webVersion,
			string domain,
			out Uri downloadUri,
			out string? error)
		{
			downloadUri = null!;
			error = null;

			if (!TryGetFileName(webLink, out var fileName, out error))
			{
				return false;
			}

			if (!TryGetVersionString(webVersion, webLink, out var version, out error))
			{
				return false;
			}

			var scheme = "https";
			if (Uri.TryCreate(webLink, UriKind.Absolute, out var baseUri))
			{
				scheme = baseUri.Scheme;
			}

			var host = domain.Trim();
			if (Uri.TryCreate(host, UriKind.Absolute, out var domainUri))
			{
				host = domainUri.Host;
			}

			if (string.IsNullOrWhiteSpace(host))
			{
				error = "Domain is empty.";
				return false;
			}

			var builder = new UriBuilder(scheme, host)
			{
				Path = $"/{version}/{fileName}",
				Query = string.Empty,
				Fragment = string.Empty
			};

			downloadUri = builder.Uri;
			return true;
		}
	}
}

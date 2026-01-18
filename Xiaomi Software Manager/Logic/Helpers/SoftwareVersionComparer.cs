using System;

namespace xsm.Logic.Helpers
{
	public enum VersionComparisonResult
	{
		Unknown,
		Equal,
		LocalNewer,
		WebNewer
	}

	public static class SoftwareVersionComparer
	{
		public static VersionComparisonResult Compare(string? webVersion, string? localVersion)
		{
			if (string.IsNullOrWhiteSpace(webVersion) || string.IsNullOrWhiteSpace(localVersion))
			{
				return VersionComparisonResult.Unknown;
			}

			if (SoftwareVersion.TryParse(webVersion, out var webParsed) &&
				SoftwareVersion.TryParse(localVersion, out var localParsed))
			{
				var compare = localParsed.CompareTo(webParsed);
				if (compare == 0)
				{
					return VersionComparisonResult.Equal;
				}

				return compare > 0 ? VersionComparisonResult.LocalNewer : VersionComparisonResult.WebNewer;
			}

			return string.Equals(webVersion, localVersion, StringComparison.OrdinalIgnoreCase)
				? VersionComparisonResult.Equal
				: VersionComparisonResult.Unknown;
		}

		public static bool IsUpToDate(string? webVersion, string? localVersion)
		{
			var result = Compare(webVersion, localVersion);
			return result == VersionComparisonResult.Equal || result == VersionComparisonResult.LocalNewer;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace xsm.Logic.Helpers
{
	public static class FolderSourceHelpers
	{
		// TODO: Add a test for this to GeneralDatabaseTests
		// Functionality haven't been tested yet
		public static string FindLatestSoftwareVersionInPath(List<string> folders)
		{
			if (folders.Count == 0)
			{
				return string.Empty;
			}

			var versions = new List<SoftwareVersion>();

			foreach (var folder in folders)
			{
				if (string.IsNullOrWhiteSpace(folder) || folder.StartsWith("_", StringComparison.Ordinal))
				{
					continue;
				}

				if (SoftwareVersion.TryParse(folder, out var version))
				{
					versions.Add(version);
				}
			}

			if (versions.Count == 0)
			{
				return string.Empty;
			}

			return versions
				.OrderByDescending(version => version)
				.First()
				.Raw;
		}
	}
}

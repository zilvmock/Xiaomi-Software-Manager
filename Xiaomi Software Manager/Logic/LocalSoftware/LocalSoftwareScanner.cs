using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using xsm.Logic.Helpers;

namespace xsm.Logic.LocalSoftware
{
	public static class LocalSoftwareScanner
	{
		public static string NormalizeModelName(string modelName)
		{
			return string.IsNullOrWhiteSpace(modelName)
				? string.Empty
				: modelName.Replace("/", " - ", StringComparison.Ordinal).Trim();
		}

		public static string BuildModelFolderName(string modelName, string regionAcronym)
		{
			var normalized = NormalizeModelName(modelName);
			return string.IsNullOrWhiteSpace(regionAcronym)
				? normalized
				: $"{normalized} {regionAcronym}".Trim();
		}

		public static bool TryGetModelFolderPath(string rootPath, string modelName, string regionAcronym, out string modelFolderPath)
		{
			modelFolderPath = string.Empty;
			if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
			{
				return false;
			}

			var expectedFolderName = BuildModelFolderName(modelName, regionAcronym);
			if (string.IsNullOrWhiteSpace(expectedFolderName))
			{
				return false;
			}

			var expectedPath = Path.Combine(rootPath, expectedFolderName);
			if (Directory.Exists(expectedPath))
			{
				modelFolderPath = expectedPath;
				return true;
			}

			var match = Directory.EnumerateDirectories(rootPath)
				.FirstOrDefault(folder =>
					string.Equals(Path.GetFileName(folder), expectedFolderName, StringComparison.OrdinalIgnoreCase));

			if (string.IsNullOrWhiteSpace(match))
			{
				return false;
			}

			modelFolderPath = match;
			return true;
		}

		public static long? TryGetModelFolderSize(string rootPath, string modelName, string regionAcronym)
		{
			if (!TryGetModelFolderPath(rootPath, modelName, regionAcronym, out var modelFolderPath))
			{
				return null;
			}

			return TryGetDirectorySize(modelFolderPath);
		}

		public static long? TryGetDirectorySize(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
			{
				return null;
			}

			var size = 0L;
			foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
			{
				try
				{
					var info = new FileInfo(file);
					size += info.Length;
				}
				catch
				{
					// Ignore files that cannot be accessed.
				}
			}

			return size;
		}

		public static string GetLatestVersionInModelFolder(string modelFolderPath)
		{
			var versions = GetVersionsInModelFolder(modelFolderPath);
			if (versions.Count == 0)
			{
				return string.Empty;
			}

			return versions
				.OrderByDescending(version => version)
				.First()
				.Raw;
		}

		public static IReadOnlyList<SoftwareVersion> GetVersionsInModelFolder(string modelFolderPath)
		{
			if (string.IsNullOrWhiteSpace(modelFolderPath) || !Directory.Exists(modelFolderPath))
			{
				return Array.Empty<SoftwareVersion>();
			}

			var versions = new List<SoftwareVersion>();

			foreach (var directory in Directory.EnumerateDirectories(modelFolderPath))
			{
				var name = Path.GetFileName(directory);
				if (TryParseVersionFromName(name, out var version))
				{
					versions.Add(version);
				}
			}

			foreach (var file in Directory.EnumerateFiles(modelFolderPath))
			{
				if (!string.Equals(Path.GetExtension(file), ".tgz", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var name = Path.GetFileNameWithoutExtension(file);
				if (TryParseVersionFromName(name, out var version))
				{
					versions.Add(version);
				}
			}

			return versions;
		}

		public static string? GetPreferredExtractedFolderPath(string modelFolderPath, string? preferredVersion)
		{
			if (string.IsNullOrWhiteSpace(modelFolderPath) || !Directory.Exists(modelFolderPath))
			{
				return null;
			}

			if (!string.IsNullOrWhiteSpace(preferredVersion))
			{
				var directMatch = Directory.EnumerateDirectories(modelFolderPath)
					.FirstOrDefault(path =>
						string.Equals(Path.GetFileName(path), preferredVersion, StringComparison.OrdinalIgnoreCase));
				if (!string.IsNullOrWhiteSpace(directMatch))
				{
					return directMatch;
				}
			}

			SoftwareVersion? latestVersion = null;
			string? latestPath = null;
			foreach (var directory in Directory.EnumerateDirectories(modelFolderPath))
			{
				var name = Path.GetFileName(directory);
				if (!SoftwareVersion.TryParse(name, out var version))
				{
					continue;
				}

				if (latestVersion == null || version.CompareTo(latestVersion) > 0)
				{
					latestVersion = version;
					latestPath = directory;
				}
			}

			return latestPath;
		}

		private static bool TryParseVersionFromName(string? name, out SoftwareVersion version)
		{
			version = null!;
			if (string.IsNullOrWhiteSpace(name) || name.StartsWith("_", StringComparison.Ordinal))
			{
				return false;
			}

			return SoftwareVersion.TryParse(name, out version);
		}
	}
}

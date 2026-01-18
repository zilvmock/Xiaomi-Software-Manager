using System;
using System.IO;

namespace xsm.Logic.LocalSoftware
{
	public sealed record LocalSoftwareStats(long? DriveTotalBytes, long? DriveFreeBytes, long? FolderSizeBytes);

	public static class LocalSoftwareStatsProvider
	{
		public static LocalSoftwareStats GetStats(string? localSoftwarePath)
		{
			if (string.IsNullOrWhiteSpace(localSoftwarePath) || !Directory.Exists(localSoftwarePath))
			{
				return new LocalSoftwareStats(null, null, null);
			}

			long? driveTotal = null;
			long? driveFree = null;

			try
			{
				var root = Path.GetPathRoot(localSoftwarePath);
				if (!string.IsNullOrWhiteSpace(root))
				{
					var drive = new DriveInfo(root);
					driveTotal = drive.TotalSize;
					driveFree = drive.AvailableFreeSpace;
				}
			}
			catch
			{
				driveTotal = null;
				driveFree = null;
			}

			long? folderSize = null;
			try
			{
				folderSize = GetDirectorySize(localSoftwarePath);
			}
			catch
			{
				folderSize = null;
			}

			return new LocalSoftwareStats(driveTotal, driveFree, folderSize);
		}

		private static long GetDirectorySize(string path)
		{
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
	}
}

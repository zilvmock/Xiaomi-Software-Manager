using System;

namespace xsm.Logic.Helpers
{
	public static class ByteSizeFormatter
	{
		private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

		public static string FormatBytes(long? bytes)
		{
			if (!bytes.HasValue || bytes.Value < 0)
			{
				return "--";
			}

			return FormatBytes(bytes.Value);
		}

		public static string FormatBytes(long bytes)
		{
			return FormatBytes((double)bytes);
		}

		public static string FormatBytes(double bytes)
		{
			if (double.IsNaN(bytes) || double.IsInfinity(bytes) || bytes < 0)
			{
				return "--";
			}

			var unitIndex = 0;
			while (bytes >= 1024 && unitIndex < Units.Length - 1)
			{
				bytes /= 1024;
				unitIndex++;
			}

			return $"{bytes:0.##} {Units[unitIndex]}";
		}

		public static string FormatBytesPerSecond(double? bytesPerSecond)
		{
			if (!bytesPerSecond.HasValue)
			{
				return "--";
			}

			var formatted = FormatBytes(bytesPerSecond.Value);
			return formatted == "--" ? formatted : $"{formatted}/s";
		}
	}
}

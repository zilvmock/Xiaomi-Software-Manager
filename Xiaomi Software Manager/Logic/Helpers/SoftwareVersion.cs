using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace xsm.Logic.Helpers
{
	public sealed class SoftwareVersion : IComparable<SoftwareVersion>
	{
		private static readonly Regex VersionTagRegex = new(
			@"(?<tag>[A-Z]{1,3}\d+\.\d+\.\d+\.\d+(?:\.[A-Z0-9]+)?)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly Regex VersionPartsRegex = new(
			@"^(?<os>[A-Z]+)(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)\.(?<build>\d+)(?:\.(?<code>[A-Z0-9]+))?$",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private SoftwareVersion(
			string raw,
			string osType,
			int major,
			int minor,
			int patch,
			int build,
			string buildCode)
		{
			Raw = raw;
			OsType = osType;
			Major = major;
			Minor = minor;
			Patch = patch;
			Build = build;
			BuildCode = buildCode;
			BuildLetter = string.IsNullOrEmpty(buildCode) ? null : (char?)char.ToUpperInvariant(buildCode[0]);
		}

		public string Raw { get; }

		public string OsType { get; }

		public int Major { get; }

		public int Minor { get; }

		public int Patch { get; }

		public int Build { get; }

		public string BuildCode { get; }

		public char? BuildLetter { get; }

		public static bool TryParse(string input, out SoftwareVersion version)
		{
			version = null!;
			if (string.IsNullOrWhiteSpace(input))
			{
				return false;
			}

			var tagMatch = VersionTagRegex.Match(input);
			if (!tagMatch.Success)
			{
				return false;
			}

			var tag = tagMatch.Groups["tag"].Value;
			var partsMatch = VersionPartsRegex.Match(tag);
			if (!partsMatch.Success)
			{
				return false;
			}

			if (!int.TryParse(partsMatch.Groups["major"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) ||
				!int.TryParse(partsMatch.Groups["minor"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor) ||
				!int.TryParse(partsMatch.Groups["patch"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var patch) ||
				!int.TryParse(partsMatch.Groups["build"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var build))
			{
				return false;
			}

			var osType = partsMatch.Groups["os"].Value.ToUpperInvariant();
			var buildCode = partsMatch.Groups["code"].Value.ToUpperInvariant();

			version = new SoftwareVersion(tag, osType, major, minor, patch, build, buildCode);
			return true;
		}

		public int CompareTo(SoftwareVersion? other)
		{
			if (other is null)
			{
				return 1;
			}

			var numericCompare = CompareNumericParts(other);
			if (numericCompare != 0)
			{
				return numericCompare;
			}

			return CompareBuildCode(other);
		}

		public override string ToString() => Raw;

		private int CompareNumericParts(SoftwareVersion other)
		{
			var leftMajor = GetComparableMajor();
			var rightMajor = other.GetComparableMajor();
			if (leftMajor != rightMajor)
			{
				return leftMajor.CompareTo(rightMajor);
			}

			if (Minor != other.Minor)
			{
				return Minor.CompareTo(other.Minor);
			}

			if (Patch != other.Patch)
			{
				return Patch.CompareTo(other.Patch);
			}

			return Build.CompareTo(other.Build);
		}

		private int CompareBuildCode(SoftwareVersion other)
		{
			if (string.IsNullOrEmpty(BuildCode) || string.IsNullOrEmpty(other.BuildCode))
			{
				return 0;
			}

			return string.Compare(BuildCode, other.BuildCode, StringComparison.OrdinalIgnoreCase);
		}

		private int GetComparableMajor()
		{
			if (string.Equals(OsType, "OS", StringComparison.OrdinalIgnoreCase))
			{
				return 815 + Major;
			}

			return Major;
		}
	}
}

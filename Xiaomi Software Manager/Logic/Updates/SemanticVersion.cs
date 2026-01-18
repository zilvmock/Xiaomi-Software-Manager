using System;
using System.Collections.Generic;

namespace xsm.Logic.Updates;

public readonly struct SemanticVersion : IComparable<SemanticVersion>
{
	public int Major { get; }
	public int Minor { get; }
	public int Patch { get; }
	public IReadOnlyList<string> PreReleaseIdentifiers { get; }

	public bool IsPreRelease => PreReleaseIdentifiers.Count > 0;

	public SemanticVersion(int major, int minor, int patch, IReadOnlyList<string> preReleaseIdentifiers)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
		PreReleaseIdentifiers = preReleaseIdentifiers;
	}

	public static bool TryParse(string? input, out SemanticVersion version)
	{
		version = default;
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}

		var trimmed = input.Trim();
		if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
		{
			trimmed = trimmed[1..];
		}

		var buildSplit = trimmed.Split('+', 2, StringSplitOptions.RemoveEmptyEntries);
		trimmed = buildSplit.Length > 0 ? buildSplit[0] : trimmed;

		string? preRelease = null;
		var dashIndex = trimmed.IndexOf('-');
		if (dashIndex >= 0)
		{
			preRelease = trimmed[(dashIndex + 1)..];
			trimmed = trimmed[..dashIndex];
		}

		var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 3)
		{
			return false;
		}

		if (!int.TryParse(parts[0], out var major) ||
			!int.TryParse(parts[1], out var minor) ||
			!int.TryParse(parts[2], out var patch))
		{
			return false;
		}

		for (var i = 3; i < parts.Length; i++)
		{
			if (!int.TryParse(parts[i], out _))
			{
				return false;
			}
		}

		var identifiers = Array.Empty<string>();
		if (!string.IsNullOrWhiteSpace(preRelease))
		{
			identifiers = preRelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
		}

		version = new SemanticVersion(major, minor, patch, identifiers);
		return true;
	}

	public int CompareTo(SemanticVersion other)
	{
		var majorCompare = Major.CompareTo(other.Major);
		if (majorCompare != 0)
		{
			return majorCompare;
		}

		var minorCompare = Minor.CompareTo(other.Minor);
		if (minorCompare != 0)
		{
			return minorCompare;
		}

		var patchCompare = Patch.CompareTo(other.Patch);
		if (patchCompare != 0)
		{
			return patchCompare;
		}

		if (!IsPreRelease && !other.IsPreRelease)
		{
			return 0;
		}

		if (!IsPreRelease && other.IsPreRelease)
		{
			return 1;
		}

		if (IsPreRelease && !other.IsPreRelease)
		{
			return -1;
		}

		return ComparePreRelease(PreReleaseIdentifiers, other.PreReleaseIdentifiers);
	}

	public override string ToString()
	{
		var value = $"{Major}.{Minor}.{Patch}";
		if (IsPreRelease)
		{
			value += "-" + string.Join(".", PreReleaseIdentifiers);
		}

		return value;
	}

	private static int ComparePreRelease(IReadOnlyList<string> left, IReadOnlyList<string> right)
	{
		var max = Math.Max(left.Count, right.Count);
		for (var i = 0; i < max; i++)
		{
			if (i >= left.Count)
			{
				return -1;
			}

			if (i >= right.Count)
			{
				return 1;
			}

			var leftId = left[i];
			var rightId = right[i];
			var leftIsNumber = int.TryParse(leftId, out var leftNumber);
			var rightIsNumber = int.TryParse(rightId, out var rightNumber);

			if (leftIsNumber && rightIsNumber)
			{
				var numericCompare = leftNumber.CompareTo(rightNumber);
				if (numericCompare != 0)
				{
					return numericCompare;
				}

				continue;
			}

			if (leftIsNumber != rightIsNumber)
			{
				return leftIsNumber ? -1 : 1;
			}

			var stringCompare = string.Compare(leftId, rightId, StringComparison.Ordinal);
			if (stringCompare != 0)
			{
				return stringCompare;
			}
		}

		return 0;
	}
}

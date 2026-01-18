using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fastenshtein;

namespace xsm.Logic.Helpers
{
	public static class LogicHelpers
	{
		/// <summary>
		/// Gets a substring from the beginning of the string until the specified word.
		/// </summary>
		/// <returns><see cref="string"/> or null</returns>
		public static string? GetSubstring(string str, string word)
		{
			var index = str.IndexOf(word, StringComparison.OrdinalIgnoreCase);
			var result = index > 0 ? str.Substring(0, index).Trim() : null;
			return result;
		}

		/// <summary>
		/// Gets the closest match to the specified word in the string using Levenshtein Distance algorithm.
		/// <para>
		/// Levenshtein calculates the shortest possible distance between two strings.
		///	Producing a count of the number of insertions, deletions and substitutions to make one string into another.
		/// </para>
		/// </summary>
		/// <param name="str"></param>
		/// <param name="word"></param>
		/// <returns>The closest matching <see cref="string"/></returns>
		public static string GetClosestMatch(string str, string word)
		{
			var words = str.Split(' ');
			var comparer = new Levenshtein(word);
			return words.OrderBy(word => comparer.DistanceFrom(word)).First();
		}

		/// <summary>
		/// Gets a substring from the first word to the second word.
		/// </summary>
		/// <param name="str"></param>
		/// <param name="firstWord"></param>
		/// <param name="secondWord"></param>
		/// <returns><see cref="string"/> or null</returns>
		public static string? GetStringBetweenWords(string str, string firstWord, string secondWord)
		{
			var firstIndex = str.IndexOf(firstWord, StringComparison.OrdinalIgnoreCase);
			var secondIndex = str.IndexOf(secondWord, StringComparison.OrdinalIgnoreCase);

			if (firstIndex < 0 || secondIndex < 0)
				return null;

			var startIndex = firstIndex + firstWord.Length;
			var length = secondIndex - startIndex;

			return length > 0 ? str.Substring(startIndex, length).Trim() : null;
		}

		/// <summary>
		/// Checks if the input is an acronym.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="threshold"></param>
		/// <returns>True if the input is an acronym</returns>
		public static bool IsAcronym(string input, double threshold = 0.7)
		{
			if (string.IsNullOrEmpty(input) || !input.All(char.IsLetter)) { return false; }

			int uppercaseCount = 0;
			int totalLetters = input.Length;

			foreach (char c in input)
			{
				if (char.IsUpper(c))
				{
					uppercaseCount++;
				}
			}

			double uppercaseRatio = (double)uppercaseCount / totalLetters;
			return uppercaseRatio >= threshold;
		}

		/// <summary>
		/// Extracts the exact software version from a string.
		/// </summary>
		/// <param name="str"></param>
		/// <returns>A match or an empty <see cref="string"/></returns>
		public static string ExtractExactVersion(string str)
		{
			return SoftwareVersion.TryParse(str, out var version) ? version.Raw : string.Empty;
		}

		/// <summary>
		/// Extracts the software version from a string.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string ExtractVersion(string str)
		{
			if (!SoftwareVersion.TryParse(str, out var version))
			{
				return string.Empty;
			}

			return $"{version.Major}.{version.Minor}.{version.Patch}.{version.Build}";
		}

		/// <summary>
		/// Extracts the region from a string.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string ExtractRegion(string str)
		{
			string pattern = @"_(.*?)_";
			Match match = Regex.Match(str, pattern);

			if (match.Success) { return match.Groups[1].Value; }
			return string.Empty;
		}

		/// <summary>
		/// Defines the software OS type. For example: OS for HyperOS
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string GetSoftwareOsType(string str)
		{
			return SoftwareVersion.TryParse(str, out var version) ? version.OsType : string.Empty;
		}

		/// <summary>
		/// Gets the first letter of the software version.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string GetFirstSoftwareVersionLetter(string str)
		{
			if (!SoftwareVersion.TryParse(str, out var version) || version.BuildLetter == null)
			{
				return string.Empty;
			}

			return version.BuildLetter.Value.ToString();
		}

		/// <summary>
		/// Gets the software version as an integer value.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static int GetSoftwareVersionIntValue(string str)
		{
			try
			{
				if (!SoftwareVersion.TryParse(str, out var version))
					return 0;

				int[] versionParts =
				{
					version.Major,
					version.Minor,
					version.Patch,
					version.Build
				};
				int versionInt = 0;

				foreach (int part in versionParts)
				{
					versionInt = versionInt * 100 + part;
				}

				return versionInt;
			}
			catch
			{
				return 0;
			}
		}
	}
}

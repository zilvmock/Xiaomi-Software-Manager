using System;
using System.Text.RegularExpressions;
using Fastenshtein;

namespace xsm.Logic.Scraper.Parsing
{
	internal static class ScrapeTextNormalizer
	{
		private static readonly (Regex pattern, string replacement)[] KnownTypos =
		{
			(new Regex(@"Lates\s*t", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Latest"),
			(new Regex(@"Giobal", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Global")
		};

		public static string NormalizeLinkText(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			var normalized = text.Replace("★", string.Empty);

			foreach (var (pattern, replacement) in KnownTypos)
			{
				normalized = pattern.Replace(normalized, replacement);
			}

			normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
			normalized = ReplaceFuzzyToken(normalized, "Latest");
			normalized = ReplaceFuzzyToken(normalized, "Version");
			normalized = ReplaceFuzzyToken(normalized, "Stable");

			return normalized;
		}

		public static string NormalizeRegionToken(string token)
		{
			if (string.IsNullOrWhiteSpace(token))
			{
				return string.Empty;
			}

			var normalized = token.Replace("Stable", string.Empty, StringComparison.OrdinalIgnoreCase);
			normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
			return normalized;
		}

		private static string ReplaceFuzzyToken(string text, string token)
		{
			var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (words.Length == 0)
			{
				return text;
			}

			var comparer = new Levenshtein(token.ToLowerInvariant());

			for (var i = 0; i < words.Length; i++)
			{
				var cleaned = Regex.Replace(words[i], @"[^A-Za-z]", string.Empty);
				if (string.IsNullOrEmpty(cleaned))
				{
					continue;
				}

				if (Math.Abs(cleaned.Length - token.Length) > 2)
				{
					continue;
				}

				var distance = comparer.DistanceFrom(cleaned.ToLowerInvariant());
				var maxDistance = Math.Max(1, token.Length / 4);
				if (distance <= maxDistance)
				{
					words[i] = token;
				}
			}

			return string.Join(" ", words);
		}
	}
}

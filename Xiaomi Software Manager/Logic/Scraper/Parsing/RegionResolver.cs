using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fastenshtein;
using xsm.Logic.Helpers;
using xsm.Models;

namespace xsm.Logic.Scraper.Parsing
{
	internal sealed class RegionResolver
	{
		private readonly IReadOnlyDictionary<string, string> _regionReference;

		public RegionResolver(IReadOnlyDictionary<string, string> regionReference)
		{
			_regionReference = regionReference;
		}

		public bool TryResolveForSeeding(string regionToken, out Region? region, out string? reason)
		{
			region = null;
			var normalized = ScrapeTextNormalizer.NormalizeRegionToken(regionToken);

			if (string.IsNullOrEmpty(normalized))
			{
				reason = "Region token is empty.";
				return false;
			}

			if (TryResolveAcronym(normalized, _regionReference.Keys, out var acronym))
			{
				if (_regionReference.TryGetValue(acronym, out var name))
				{
					region = new Region { Name = name, Acronym = acronym };
					reason = null;
					return true;
				}

				reason = $"Region acronym '{acronym}' not in reference list.";
				return false;
			}

			if (TryResolveName(normalized, _regionReference.Values, out var resolvedName))
			{
				var key = _regionReference.FirstOrDefault(pair =>
					string.Equals(pair.Value, resolvedName, StringComparison.OrdinalIgnoreCase)).Key;

				if (!string.IsNullOrWhiteSpace(key))
				{
					region = new Region { Name = resolvedName, Acronym = key };
					reason = null;
					return true;
				}
			}

			reason = $"Region '{normalized}' not in reference list.";
			return false;
		}

		public bool TryResolveForSoftware(string regionToken, IReadOnlyList<Region> regions, out string? acronym, out string? reason)
		{
			acronym = null;
			var normalized = ScrapeTextNormalizer.NormalizeRegionToken(regionToken);

			if (string.IsNullOrEmpty(normalized))
			{
				reason = "Region token is empty.";
				return false;
			}

			if (TryResolveAcronym(normalized, regions.Select(r => r.Acronym), out var acronymCandidate))
			{
				if (regions.Any(r => string.Equals(r.Acronym, acronymCandidate, StringComparison.OrdinalIgnoreCase)))
				{
					acronym = acronymCandidate;
					reason = null;
					return true;
				}

				reason = $"Region acronym '{acronymCandidate}' not in seeded list.";
				return false;
			}

			if (TryResolveName(normalized, regions.Select(r => r.Name), out var resolvedName))
			{
				var region = regions.FirstOrDefault(r =>
					string.Equals(r.Name, resolvedName, StringComparison.OrdinalIgnoreCase));

				if (region != null)
				{
					acronym = region.Acronym;
					reason = null;
					return true;
				}
			}

			reason = $"Region '{normalized}' not in seeded list.";
			return false;
		}

		private static bool TryResolveAcronym(string token, IEnumerable<string> knownAcronyms, out string acronym)
		{
			acronym = string.Empty;
			var lettersOnly = new string(token.Where(char.IsLetter).ToArray());
			if (string.IsNullOrEmpty(lettersOnly))
			{
				return false;
			}

			if (!LogicHelpers.IsAcronym(lettersOnly) && lettersOnly.Length > 4)
			{
				return false;
			}

			acronym = lettersOnly.ToUpperInvariant();
			return knownAcronyms.Contains(acronym, StringComparer.OrdinalIgnoreCase);
		}

		private static bool TryResolveName(string token, IEnumerable<string> candidates, out string resolvedName)
		{
			resolvedName = string.Empty;
			var normalizedToken = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(token.ToLowerInvariant());

			var directMatch = candidates.FirstOrDefault(name =>
				string.Equals(name, normalizedToken, StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrWhiteSpace(directMatch))
			{
				resolvedName = directMatch;
				return true;
			}

			if (TryFindClosestMatch(normalizedToken, candidates, out var closest))
			{
				resolvedName = closest;
				return true;
			}

			return false;
		}

		private static bool TryFindClosestMatch(string input, IEnumerable<string> candidates, out string match)
		{
			match = string.Empty;
			var normalizedInput = input.Trim().ToLowerInvariant();
			if (string.IsNullOrEmpty(normalizedInput))
			{
				return false;
			}

			var comparer = new Levenshtein(normalizedInput);
			var best = candidates
				.Select(candidate =>
				{
					var normalizedCandidate = candidate.ToLowerInvariant();
					var distance = comparer.DistanceFrom(normalizedCandidate);
					return (candidate, normalizedCandidate, distance);
				})
				.OrderBy(result => result.distance)
				.FirstOrDefault();

			if (string.IsNullOrWhiteSpace(best.candidate))
			{
				return false;
			}

			var maxDistance = Math.Max(1, Math.Min(normalizedInput.Length, best.normalizedCandidate.Length) / 4);
			if (best.distance <= maxDistance)
			{
				match = best.candidate;
				return true;
			}

			return false;
		}
	}
}

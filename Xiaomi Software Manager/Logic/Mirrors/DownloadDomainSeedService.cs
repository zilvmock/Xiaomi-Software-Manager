using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data;
using xsm.Models;

namespace xsm.Logic.Mirrors;

public static class DownloadDomainSeedService
{
	private const string SeedFileName = "download-domains.json";
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public static string GetDefaultSeedPath()
	{
		return Path.Combine(AppContext.BaseDirectory, "Data", "Seeds", SeedFileName);
	}

	public static async Task<SeedSummary> SeedAsync(AppDbContext context, string? seedPath, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		var path = string.IsNullOrWhiteSpace(seedPath) ? GetDefaultSeedPath() : seedPath;
		if (!File.Exists(path))
		{
			return new SeedSummary(path, 0, 0, 0, 0, false);
		}

		var json = await File.ReadAllTextAsync(path, cancellationToken);
		var seed = JsonSerializer.Deserialize<DownloadDomainSeedFile>(json, JsonOptions);
		var entries = seed?.Domains ?? new List<DownloadDomainSeedEntry>();

		var normalized = entries
			.Where(entry => !string.IsNullOrWhiteSpace(entry.Domain))
			.GroupBy(entry => entry.Domain.Trim(), StringComparer.OrdinalIgnoreCase)
			.Select(group => group.Last())
			.ToList();

		var existing = await context.DownloadDomains
			.ToListAsync(cancellationToken);
		var existingByDomain = existing
			.ToDictionary(item => item.Domain, StringComparer.OrdinalIgnoreCase);

		var added = 0;
		var updated = 0;

		foreach (var entry in normalized)
		{
			if (existingByDomain.TryGetValue(entry.Domain.Trim(), out var domain))
			{
				domain.Type = entry.Type;
				domain.PrimaryRegion = entry.PrimaryRegion;
				domain.Infrastructure = entry.Infrastructure;
				domain.OptimizationPriority = entry.OptimizationPriority;
				updated++;
				continue;
			}

			context.DownloadDomains.Add(new DownloadDomain
			{
				Domain = entry.Domain.Trim(),
				Type = entry.Type,
				PrimaryRegion = entry.PrimaryRegion,
				Infrastructure = entry.Infrastructure,
				OptimizationPriority = entry.OptimizationPriority
			});
			added++;
		}

		var seedDomains = new HashSet<string>(normalized.Select(entry => entry.Domain.Trim()), StringComparer.OrdinalIgnoreCase);
		var removed = existing
			.Where(item => !seedDomains.Contains(item.Domain))
			.ToList();

		if (removed.Count > 0)
		{
			context.DownloadDomains.RemoveRange(removed);
		}

		await context.SaveChangesAsync(cancellationToken);

		return new SeedSummary(path, normalized.Count, added, updated, removed.Count, true);
	}

	public sealed record SeedSummary(string Path, int Total, int Added, int Updated, int Removed, bool Loaded);

	private sealed class DownloadDomainSeedFile
	{
		public List<DownloadDomainSeedEntry> Domains { get; set; } = new();
	}

		private sealed class DownloadDomainSeedEntry
		{
			public string Domain { get; set; } = string.Empty;
			public string Type { get; set; } = string.Empty;
			public string PrimaryRegion { get; set; } = string.Empty;
			public string Infrastructure { get; set; } = string.Empty;
			public string OptimizationPriority { get; set; } = string.Empty;
		}
	}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data;
using xsm.Logic.Helpers;

namespace xsm.Logic.LocalSoftware
{
	public sealed record LocalSoftwareSyncSummary(int Total, int Updated, int Missing);

	public sealed class LocalSoftwareSyncService
	{
		public async Task<LocalSoftwareSyncSummary> RefreshAllAsync(string? localSoftwarePath, CancellationToken cancellationToken = default)
		{
			await using var context = AppDbContextFactory.Create();
			await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

			var softwareItems = await context.Software
				.Include(item => item.Regions)
				.ToListAsync(cancellationToken);

			Dictionary<string, string>? folderLookup = null;
			if (!string.IsNullOrWhiteSpace(localSoftwarePath) && Directory.Exists(localSoftwarePath))
			{
				folderLookup = Directory.EnumerateDirectories(localSoftwarePath)
					.Select(path => new { Path = path, Name = Path.GetFileName(path) })
					.Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
					.ToDictionary(entry => entry.Name!, entry => entry.Path, StringComparer.OrdinalIgnoreCase);
			}

			var updated = 0;
			var missing = 0;

			foreach (var software in softwareItems)
			{
				var region = software.Regions.FirstOrDefault()?.Acronym;
				if (string.IsNullOrWhiteSpace(region))
				{
					missing++;
					continue;
				}

				var latestLocalVersion = string.Empty;
				if (folderLookup != null)
				{
					var folderName = LocalSoftwareScanner.BuildModelFolderName(software.Name, region);
					if (folderLookup.TryGetValue(folderName, out var modelFolderPath))
					{
						latestLocalVersion = LocalSoftwareScanner.GetLatestVersionInModelFolder(modelFolderPath);
					}
				}

				if (string.IsNullOrWhiteSpace(latestLocalVersion))
				{
					missing++;
				}

				if (!string.Equals(software.LocalVersion, latestLocalVersion, StringComparison.OrdinalIgnoreCase))
				{
					software.LocalVersion = latestLocalVersion;
					software.IsUpToDate = SoftwareVersionComparer.IsUpToDate(software.WebVersion, latestLocalVersion);
					updated++;
					continue;
				}

				software.IsUpToDate = SoftwareVersionComparer.IsUpToDate(software.WebVersion, latestLocalVersion);
			}

			await context.SaveChangesAsync(cancellationToken);
			return new LocalSoftwareSyncSummary(softwareItems.Count, updated, missing);
		}
	}
}

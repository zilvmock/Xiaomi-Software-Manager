using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data;

namespace xsm.Logic.Mirrors;

public sealed class MirrorSelector
{
	public async Task<Uri?> SelectBestMirrorAsync(string originalUrl, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(originalUrl))
		{
			return null;
		}

		return Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri)
			? await SelectBestMirrorAsync(uri, cancellationToken)
			: null;
	}

	public async Task<Uri?> SelectBestMirrorAsync(Uri originalUri, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(originalUri);

		await using var context = AppDbContextFactory.Create();
		await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

		var best = await context.DownloadDomains
			.AsNoTracking()
			.Where(domain => domain.LastScore.HasValue)
			.Where(domain => domain.LastStatus == MirrorStatus.Healthy)
			.OrderByDescending(domain => domain.LastScore)
			.FirstOrDefaultAsync(cancellationToken);

		if (best == null)
		{
			return originalUri;
		}

		return MirrorUrlBuilder.TryBuildMirrorUri(originalUri, best.Domain, out var mirrorUri, out _)
			? mirrorUri
			: originalUri;
	}
}

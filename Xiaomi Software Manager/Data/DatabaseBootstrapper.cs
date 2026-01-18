using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace xsm.Data;

public static class DatabaseBootstrapper
{
	private static readonly TimeSpan SchemaTimeout = TimeSpan.FromSeconds(15);

	public static async Task EnsureSchemaAsync(AppDbContext context, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(context);

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(SchemaTimeout);

		try
		{
			await context.Database.MigrateAsync(timeoutCts.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException($"Database schema initialization exceeded {SchemaTimeout.TotalSeconds:0} seconds.");
		}
	}
}

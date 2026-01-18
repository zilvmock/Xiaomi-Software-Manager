using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Data;
using xsm.Data.Repositories;
using xsm.Models;

namespace xsm.Logic.Mirrors;

public sealed class MirrorRatingRunner
{
	private const string LastRatingSettingKey = "mirrors.last_rating_utc";
	private static readonly Lazy<MirrorRatingRunner> LazyInstance = new(() => new MirrorRatingRunner());
	private readonly SemaphoreSlim _gate = new(1, 1);
	private Task<MirrorRatingSummary>? _activeTask;
	private CancellationTokenSource? _cts;

	public static MirrorRatingRunner Instance => LazyInstance.Value;

	public Task<MirrorRatingSummary> StartRatingAsync(bool force, CancellationToken cancellationToken = default)
	{
		return StartOrReuseAsync(force, cancellationToken);
	}

	public Task WaitForCompletionAsync()
	{
		return _activeTask ?? Task.CompletedTask;
	}

	public bool RequestStop()
	{
		try
		{
			var cts = _cts;
			if (cts == null || cts.IsCancellationRequested)
			{
				return false;
			}

			cts.Cancel();
			return true;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	private async Task<MirrorRatingSummary> StartOrReuseAsync(bool force, CancellationToken cancellationToken)
	{
		Task<MirrorRatingSummary>? task = null;
		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (_activeTask != null && !_activeTask.IsCompleted)
			{
				Logger.Instance.Log("Mirror rating already running.", LogLevel.Warning);
				task = _activeTask;
			}
			else
			{
				_cts?.Dispose();
				_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				task = _activeTask = RunAsync(force, _cts.Token);
			}
		}
		finally
		{
			_gate.Release();
		}

		return task == null ? new MirrorRatingSummary(false, "No task created.", 0, 0, 0) : await task;
	}

	private async Task<MirrorRatingSummary> RunAsync(bool force, CancellationToken cancellationToken)
	{
		var startTime = DateTime.UtcNow;
		var progressEntry = new LogEntry("Mirror rating in progress", "Elapsed: 0s", level: LogLevel.Task);
		var progressLog = Logger.Instance.Log(progressEntry);
		using var timerCts = new CancellationTokenSource();
		_ = TimerAsync(startTime, progressEntry, timerCts.Token);

		try
		{
			await using var context = AppDbContextFactory.Create();
			await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

			var seedSummary = await DownloadDomainSeedService.SeedAsync(context, null, cancellationToken);
			progressLog.AddDetail("Seed file", seedSummary.Path, LogLevel.Debug);
			progressLog.AddDetail("Domains in seed", seedSummary.Total.ToString(), LogLevel.Debug);
			progressLog.AddDetail("Added", seedSummary.Added.ToString(), LogLevel.Debug);
			progressLog.AddDetail("Updated", seedSummary.Updated.ToString(), LogLevel.Debug);
			progressLog.AddDetail("Removed", seedSummary.Removed.ToString(), LogLevel.Debug);

			var settingsRepository = new AppSettingRepository(context);
			var lastRating = await settingsRepository.GetByKeyAsync(LastRatingSettingKey);
			if (!force && TryParseTimestamp(lastRating?.Value, out var lastRun) &&
				DateTimeOffset.UtcNow - lastRun < TimeSpan.FromDays(1))
			{
				progressLog.AddDetail("Mirror rating skipped", "Last run within 24 hours.", LogLevel.Info);
				UpdateProgressEntry(progressEntry, (int)(DateTime.UtcNow - startTime).TotalSeconds, "Skipped");
				return new MirrorRatingSummary(false, "Last run within 24 hours.", 0, 0, 0);
			}

			var testUri = await GetTestUriAsync(context, cancellationToken);
			if (testUri == null)
			{
				progressLog.AddDetail("Mirror rating skipped", "No software download URL found.", LogLevel.Warning);
				UpdateProgressEntry(progressEntry, (int)(DateTime.UtcNow - startTime).TotalSeconds, "Skipped");
				return new MirrorRatingSummary(false, "No software download URL found.", 0, 0, 0);
			}

			progressLog.AddDetail("Test URL", testUri.ToString(), LogLevel.Debug);

			var domains = await context.DownloadDomains.ToListAsync(cancellationToken);
			if (domains.Count == 0)
			{
				progressLog.AddDetail("Mirror rating skipped", "No download mirrors configured.", LogLevel.Warning);
				UpdateProgressEntry(progressEntry, (int)(DateTime.UtcNow - startTime).TotalSeconds, "Skipped");
				return new MirrorRatingSummary(false, "No download mirrors configured.", 0, 0, 0);
			}

			var results = await RateDomainsAsync(domains, testUri, progressLog, cancellationToken);
			var now = DateTimeOffset.UtcNow;
			foreach (var result in results)
			{
				var domain = domains.FirstOrDefault(item =>
					string.Equals(item.Domain, result.Domain, StringComparison.OrdinalIgnoreCase));
				if (domain == null)
				{
					continue;
				}

				domain.LastRatedAt = now;
				domain.LastStatus = result.Status;
				domain.LastError = result.ErrorMessage;
				domain.LastThroughputMBps = result.ThroughputMBps;
				domain.LastLatencyMs = result.LatencyMs;
				domain.LastJitterMs = result.JitterMs;
				domain.LastScore = result.Score;
			}

			await context.SaveChangesAsync(cancellationToken);
			await settingsRepository.SetAsync(LastRatingSettingKey, now.ToString("O"));

			var healthyCount = results.Count(result => result.Status == MirrorStatus.Healthy);
			progressLog.AddDetail("Mirrors tested", results.Count.ToString());
			progressLog.AddDetail("Healthy mirrors", healthyCount.ToString());
			UpdateProgressEntry(progressEntry, (int)(DateTime.UtcNow - startTime).TotalSeconds, "Completed");

			return new MirrorRatingSummary(true, null, results.Count, healthyCount, domains.Count);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			progressLog.AddDetail("Mirror rating canceled.", level: LogLevel.Warning);
			UpdateProgressEntry(progressEntry, (int)(DateTime.UtcNow - startTime).TotalSeconds, "Canceled");
			return new MirrorRatingSummary(false, "Canceled.", 0, 0, 0);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			progressLog.AddDetail("Mirror rating canceled.", level: LogLevel.Warning);
			UpdateProgressEntry(progressEntry, (int)(DateTime.UtcNow - startTime).TotalSeconds, "Canceled");
			return new MirrorRatingSummary(false, "Canceled.", 0, 0, 0);
		}
		catch (Exception ex)
		{
			progressLog.AddDetail("Mirror rating failed.", ex.Message, LogLevel.Error);
			AddExceptionDetails(progressLog, ex);
			UpdateProgressEntry(progressEntry, (int)(DateTime.UtcNow - startTime).TotalSeconds, "Failed");
			return new MirrorRatingSummary(false, ex.Message, 0, 0, 0);
		}
		finally
		{
			timerCts.Cancel();
		}
	}

	private static async Task<List<MirrorProbe.MirrorProbeResult>> RateDomainsAsync(
		IReadOnlyList<DownloadDomain> domains,
		Uri testUri,
		Logger.LogHandle logHandle,
		CancellationToken cancellationToken)
	{
		using var handler = new SocketsHttpHandler
		{
			AllowAutoRedirect = true
		};
		using var client = new HttpClient(handler)
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
		client.DefaultRequestVersion = HttpVersion.Version20;
		client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

		var probe = new MirrorProbe(client);
		var tasks = domains
			.Select(domain => RateSingleDomainAsync(domain, testUri, probe, logHandle, cancellationToken))
			.ToList();

		var results = await Task.WhenAll(tasks);
		return results.ToList();
	}

	private static async Task<MirrorProbe.MirrorProbeResult> RateSingleDomainAsync(
		DownloadDomain domain,
		Uri testUri,
		MirrorProbe probe,
		Logger.LogHandle logHandle,
		CancellationToken cancellationToken)
	{
		if (!MirrorUrlBuilder.TryBuildMirrorUri(testUri, domain.Domain, out var mirrorUri, out var error))
		{
			logHandle.AddDetail(domain.Domain, error, LogLevel.Warning);
			return new MirrorProbe.MirrorProbeResult(domain.Domain, MirrorStatus.Error, null, null, null, null, error);
		}

		var result = await probe.ProbeAsync(domain, mirrorUri, cancellationToken);
		var details = BuildDetailDescription(result);
		var level = result.Status == MirrorStatus.Healthy ? LogLevel.Info : LogLevel.Warning;
		logHandle.AddDetail(result.Domain, details, level);
		return result;
	}

	private static string BuildDetailDescription(MirrorProbe.MirrorProbeResult result)
	{
		if (result.Status == MirrorStatus.Healthy)
		{
			return $"Throughput {FormatValue(result.ThroughputMBps, "MB/s")}, " +
				$"Latency {FormatValue(result.LatencyMs, "ms")}, " +
				$"Jitter {FormatValue(result.JitterMs, "ms")}, " +
				$"Score {FormatValue(result.Score, string.Empty)}";
		}

		return string.IsNullOrWhiteSpace(result.ErrorMessage)
			? $"Status {result.Status}"
			: $"Status {result.Status}: {result.ErrorMessage}";
	}

	private static string FormatValue(double? value, string unit)
	{
		if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
		{
			return "--";
		}

		return string.IsNullOrWhiteSpace(unit)
			? value.Value.ToString("0.##")
			: $"{value.Value:0.##} {unit}";
	}

	private static async Task<Uri?> GetTestUriAsync(AppDbContext context, CancellationToken cancellationToken)
	{
		var url = await context.Software
			.AsNoTracking()
			.Where(software => !string.IsNullOrWhiteSpace(software.WebLink))
			.Select(software => software.WebLink)
			.FirstOrDefaultAsync(cancellationToken);

		if (string.IsNullOrWhiteSpace(url))
		{
			return null;
		}

		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return null;
		}

		var builder = new UriBuilder(uri)
		{
			Query = string.Empty,
			Fragment = string.Empty
		};

		return builder.Uri;
	}

	private static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
	{
		if (!string.IsNullOrWhiteSpace(value) && DateTimeOffset.TryParse(value, out var parsed))
		{
			timestamp = parsed;
			return true;
		}

		timestamp = default;
		return false;
	}

	private static async Task TimerAsync(DateTime startTime, LogEntry entry, CancellationToken cancellationToken)
	{
		var lastReported = -1;
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var elapsedSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;
				if (elapsedSeconds != lastReported)
				{
					lastReported = elapsedSeconds;
					UpdateProgressEntry(entry, elapsedSeconds, null);
				}

				await Task.Delay(1000, cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{
			// Ignore cancellations from shutdown.
		}
	}

	private static void UpdateProgressEntry(LogEntry entry, int elapsedSeconds, string? status)
	{
		var description = string.IsNullOrWhiteSpace(status)
			? $"Elapsed: {elapsedSeconds}s"
			: $"{status} in {elapsedSeconds} seconds";

		if (Dispatcher.UIThread.CheckAccess())
		{
			entry.UpdateDescription(description);
			return;
		}

		Dispatcher.UIThread.Post(() => entry.UpdateDescription(description));
	}

	private static void AddExceptionDetails(Logger.LogHandle logHandle, Exception exception)
	{
		var current = exception;
		var depth = 0;

		while (current != null)
		{
			var prefix = depth == 0 ? "Exception" : $"Inner exception {depth}";
			logHandle.AddDetail(prefix, current.GetType().FullName ?? "Unknown", LogLevel.Debug);
			logHandle.AddDetail("Message", current.Message, LogLevel.Error);

			if (!string.IsNullOrWhiteSpace(current.StackTrace))
			{
				logHandle.AddDetail("Stack Trace", current.StackTrace, LogLevel.Debug);
			}

			current = current.InnerException;
			depth++;
		}
	}

	public sealed record MirrorRatingSummary(bool Ran, string? Reason, int TestedCount, int HealthyCount, int TotalEligible);
}

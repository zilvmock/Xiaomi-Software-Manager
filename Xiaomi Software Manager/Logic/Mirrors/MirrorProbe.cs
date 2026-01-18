using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using xsm.Models;

namespace xsm.Logic.Mirrors;

internal sealed class MirrorProbe
{
	private const int WarmupBytes = 256 * 1024;
	private const int StandardBytes = 1024 * 1024;
	private const int RampUpBytes = 5 * 1024 * 1024;
	private const int JitterBytes = 64 * 1024;
	private const int WarmupThresholdMs = 100;
	private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan ThroughputTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan JitterTimeout = TimeSpan.FromSeconds(10);

	private const double ThroughputWeight = 0.7;
	private const double LatencyWeight = 0.2;
	private const double JitterWeight = 0.1;

	private readonly HttpClient _client;

	public MirrorProbe(HttpClient client)
	{
		_client = client;
	}

	public async Task<MirrorProbeResult> ProbeAsync(DownloadDomain domain, Uri testUri, CancellationToken cancellationToken)
	{
		var health = await CheckHealthAsync(testUri, cancellationToken);
		if (!health.Success)
		{
			return new MirrorProbeResult(domain.Domain, MirrorStatus.Unreachable, null, null, null, null, health.Error);
		}

		var throughput = await MeasureThroughputAsync(testUri, cancellationToken);
		if (throughput.Sample == null)
		{
			return new MirrorProbeResult(domain.Domain, MirrorStatus.Slow, null, null, null, null, throughput.Error);
		}

		var jitterSamples = await MeasureJitterAsync(testUri, cancellationToken);
		if (jitterSamples.Samples == null)
		{
			return new MirrorProbeResult(domain.Domain, MirrorStatus.Unreachable, null, null, null, null, jitterSamples.Error);
		}

		var latency = jitterSamples.Samples.Average();
		var jitter = CalculateStdDev(jitterSamples.Samples);
		var score = CalculateScore(throughput.Sample.ThroughputMBps, latency, jitter);

		return new MirrorProbeResult(domain.Domain, MirrorStatus.Healthy, throughput.Sample.ThroughputMBps, latency, jitter, score, null);
	}

	private async Task<(bool Success, string? Error)> CheckHealthAsync(Uri uri, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Head, AppendCacheBuster(uri));
		request.Version = HttpVersion.Version20;
		request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

		try
		{
			using var cts = CreateTimeoutToken(cancellationToken, HealthTimeout);
			using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
			if (!response.IsSuccessStatusCode)
			{
				return (false, $"HTTP {(int)response.StatusCode}");
			}

			var supportsRanges = response.Headers.AcceptRanges
				.Any(value => string.Equals(value, "bytes", StringComparison.OrdinalIgnoreCase));
			if (!supportsRanges)
			{
				return (false, "Accept-Ranges not supported");
			}

			return (true, null);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return (false, "Health check timeout");
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	private async Task<(ThroughputSample? Sample, string? Error)> MeasureThroughputAsync(Uri uri, CancellationToken cancellationToken)
	{
		var warmup = await GetThroughputSampleAsync(uri, WarmupBytes, cancellationToken);
		if (warmup.Sample == null)
		{
			return warmup;
		}

		var targetBytes = warmup.Sample.Elapsed.TotalMilliseconds < WarmupThresholdMs
			? RampUpBytes
			: StandardBytes;

		var sample = await GetThroughputSampleAsync(uri, targetBytes, cancellationToken);
		return sample.Sample == null ? sample : (sample.Sample, null);
	}

	private async Task<(ThroughputSample? Sample, string? Error)> GetThroughputSampleAsync(Uri uri, int bytes, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, AppendCacheBuster(uri));
		request.Version = HttpVersion.Version20;
		request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
		request.Headers.Range = new RangeHeaderValue(0, bytes - 1);

		try
		{
			using var cts = CreateTimeoutToken(cancellationToken, ThroughputTimeout);
			var stopwatch = Stopwatch.StartNew();
			using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
			if (!response.IsSuccessStatusCode)
			{
				return (null, $"HTTP {(int)response.StatusCode}");
			}

			await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
			var buffer = new byte[8192];
			var remaining = bytes;
			var totalRead = 0;

			while (remaining > 0)
			{
				var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cts.Token);
				if (read == 0)
				{
					break;
				}

				remaining -= read;
				totalRead += read;
			}

			stopwatch.Stop();
			if (totalRead < bytes)
			{
				return (null, $"Received {totalRead} bytes of {bytes}");
			}

			var throughput = totalRead / stopwatch.Elapsed.TotalSeconds / (1024d * 1024d);
			return (new ThroughputSample(throughput, stopwatch.Elapsed), null);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return (null, "Throughput timeout");
		}
		catch (Exception ex)
		{
			return (null, ex.Message);
		}
	}

	private async Task<(IReadOnlyList<double>? Samples, string? Error)> MeasureJitterAsync(Uri uri, CancellationToken cancellationToken)
	{
		var samples = new List<double>(3);
		for (var i = 0; i < 3; i++)
		{
			var ttfb = await MeasureTtfbAsync(uri, cancellationToken);
			if (ttfb.Value == null)
			{
				return (null, ttfb.Error);
			}

			samples.Add(ttfb.Value.Value);
		}

		return (samples, null);
	}

	private async Task<(double? Value, string? Error)> MeasureTtfbAsync(Uri uri, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, AppendCacheBuster(uri));
		request.Version = HttpVersion.Version20;
		request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
		request.Headers.Range = new RangeHeaderValue(0, JitterBytes - 1);

		try
		{
			using var cts = CreateTimeoutToken(cancellationToken, JitterTimeout);
			var stopwatch = Stopwatch.StartNew();
			using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
			if (!response.IsSuccessStatusCode)
			{
				return (null, $"HTTP {(int)response.StatusCode}");
			}

			await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
			var buffer = new byte[1];
			var read = await stream.ReadAsync(buffer.AsMemory(0, 1), cts.Token);
			if (read == 0)
			{
				return (null, "No data received");
			}

			stopwatch.Stop();
			return (stopwatch.Elapsed.TotalMilliseconds, null);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return (null, "Latency timeout");
		}
		catch (Exception ex)
		{
			return (null, ex.Message);
		}
	}

	private static CancellationTokenSource CreateTimeoutToken(CancellationToken cancellationToken, TimeSpan timeout)
	{
		var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(timeout);
		return cts;
	}

	private static Uri AppendCacheBuster(Uri uri)
	{
		var builder = new UriBuilder(uri);
		var token = $"xsm_test={Guid.NewGuid():N}";
		if (string.IsNullOrWhiteSpace(builder.Query))
		{
			builder.Query = token;
		}
		else
		{
			builder.Query = builder.Query.TrimStart('?') + "&" + token;
		}

		return builder.Uri;
	}

	private static double CalculateScore(double throughput, double latency, double jitter)
	{
		return (ThroughputWeight * throughput) - (LatencyWeight * latency) - (JitterWeight * jitter);
	}

	private static double CalculateStdDev(IReadOnlyList<double> values)
	{
		if (values.Count == 0)
		{
			return 0;
		}

		var average = values.Average();
		var variance = values.Sum(value => Math.Pow(value - average, 2)) / values.Count;
		return Math.Sqrt(variance);
	}

	private sealed record ThroughputSample(double ThroughputMBps, TimeSpan Elapsed);

	internal sealed record MirrorProbeResult(
		string Domain,
		string Status,
		double? ThroughputMBps,
		double? LatencyMs,
		double? JitterMs,
		double? Score,
		string? ErrorMessage);
}

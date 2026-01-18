using System;

namespace xsm.Models;

public sealed class DownloadDomain
{
	public int Id { get; set; }

	public string Domain { get; set; } = string.Empty;

	public string Type { get; set; } = string.Empty;

	public string PrimaryRegion { get; set; } = string.Empty;

	public string Infrastructure { get; set; } = string.Empty;

	public string OptimizationPriority { get; set; } = string.Empty;

	public DateTimeOffset? LastRatedAt { get; set; }

	public double? LastThroughputMBps { get; set; }

	public double? LastLatencyMs { get; set; }

	public double? LastJitterMs { get; set; }

	public double? LastScore { get; set; }

	public string? LastStatus { get; set; }

	public string? LastError { get; set; }
}

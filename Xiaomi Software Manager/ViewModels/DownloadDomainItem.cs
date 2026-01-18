using System;
using xsm.Logic.Mirrors;
using xsm.Models;

namespace xsm.ViewModels;

public sealed class DownloadDomainItem
{
	public DownloadDomainItem(
		string domain,
		string latencyText,
		string throughputText,
		string statusText,
		string statusColor,
		string? lastRatedText)
	{
		Domain = domain;
		LatencyText = latencyText;
		ThroughputText = throughputText;
		StatusText = statusText;
		StatusColor = statusColor;
		LastRatedText = lastRatedText;
	}

	public string Domain { get; }
	public string LatencyText { get; }
	public string ThroughputText { get; }
	public string StatusText { get; }
	public string StatusColor { get; }
	public string? LastRatedText { get; }

	public static DownloadDomainItem FromDomain(DownloadDomain domain)
	{
		var latencyText = FormatValue(domain.LastLatencyMs, "ms");
		var throughputText = FormatValue(domain.LastThroughputMBps, "MB/s");
		var statusText = string.IsNullOrWhiteSpace(domain.LastStatus) ? "Unknown" : domain.LastStatus;
		var statusColor = ResolveStatusColor(domain.LastStatus);
		var lastRatedText = domain.LastRatedAt.HasValue
			? domain.LastRatedAt.Value.ToLocalTime().ToString("g")
			: null;

		return new DownloadDomainItem(
			domain.Domain,
			latencyText,
			throughputText,
			statusText,
			statusColor,
			lastRatedText);
	}

	private static string FormatValue(double? value, string unit)
	{
		if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
		{
			return "--";
		}

		return $"{value.Value:0.##} {unit}";
	}

	private static string ResolveStatusColor(string? status)
	{
		return status switch
		{
			MirrorStatus.Healthy => "#16A34A",
			MirrorStatus.Slow => "#EAB308",
			MirrorStatus.Unreachable => "#B91C1C",
			MirrorStatus.Error => "#B91C1C",
			MirrorStatus.Skipped => "#9CA3AF",
			_ => "#9CA3AF"
		};
	}
}

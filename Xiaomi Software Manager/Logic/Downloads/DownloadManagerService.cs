using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Data;
using xsm.Data.Repositories;
using xsm.Logic;
using xsm.Logic.Helpers;
using xsm.Logic.LocalSoftware;
using xsm.Logic.Mirrors;
using xsm.Models;
using xsm.ViewModels;
using xsm.ViewModels.Windows;

namespace xsm.Logic.Downloads
{
	public sealed class DownloadManagerService
	{
		private static readonly Lazy<DownloadManagerService> LazyInstance = new(() => new DownloadManagerService());
		private readonly Dictionary<DownloadItemViewModel, DownloadJob> _jobsByItem = new();
		private readonly SemaphoreSlim _gate = new(1, 1);
		private readonly HttpClient _httpClient;
		private LocalSoftwareStats _lastStats = new(null, null, null);
		private string? _localSoftwarePath;

		public static DownloadManagerService Instance => LazyInstance.Value;

		public DownloadManagerViewModel ViewModel { get; }

		public string? LocalSoftwarePath
		{
			get => _localSoftwarePath;
			set
			{
				_localSoftwarePath = value;
				UpdateDiskStats();
			}
		}

		public bool TryGetSourceItem(DownloadItemViewModel item, out SoftwareRowViewModel source)
		{
			if (item != null && _jobsByItem.TryGetValue(item, out var job))
			{
				source = job.Source;
				return true;
			}

			source = null!;
			return false;
		}

		private DownloadManagerService()
		{
			ViewModel = new DownloadManagerViewModel();
			ViewModel.PropertyChanged += OnViewModelPropertyChanged;
			_httpClient = new HttpClient(new SocketsHttpHandler
			{
				AllowAutoRedirect = true
			})
			{
				Timeout = Timeout.InfiniteTimeSpan
			};
		}

		public async Task EnqueueDownloadsAsync(IEnumerable<SoftwareRowViewModel> selections, CancellationToken cancellationToken = default)
		{
			var items = selections?.ToList() ?? new List<SoftwareRowViewModel>();
			if (items.Count == 0)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(LocalSoftwarePath))
			{
				Logger.Instance.Log("Local software folder is not configured.", LogLevel.Warning);
				return;
			}

			await _gate.WaitAsync(cancellationToken);
			try
			{
				if (!_jobsByItem.Values.Any(job => job.IsPendingOrActive || job.IsTaskRunning))
				{
					_jobsByItem.Clear();
					UpdateOnUiThread(() => ViewModel.Items.Clear());
				}

				foreach (var selection in items)
				{
					if (!TryCreateJob(selection, out var job))
					{
						continue;
					}

					if (_jobsByItem.Values.Any(existing =>
						existing.IsPendingOrActive &&
						string.Equals(existing.FileName, job.FileName, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(existing.Version, job.Version, StringComparison.OrdinalIgnoreCase)))
					{
						Logger.Instance.Log($"Download already queued: {job.FileName}", LogLevel.Warning);
						continue;
					}

					_jobsByItem[job.ViewModel] = job;
					AddItem(job.ViewModel);
					job.SetStatus(DownloadStatus.Queued);
					Logger.Instance.Log($"Queued download: {job.FileName}", LogLevel.Info);
				}
			}
			finally
			{
				_gate.Release();
			}

			UpdateMoveStates();
			UpdateManagerState();
			await StartQueuedDownloadsAsync(cancellationToken);
		}

		public async Task ResetStaleDownloadingFlagsAsync(CancellationToken cancellationToken)
		{
			await using var context = AppDbContextFactory.Create();
			await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

			var stale = await context.Software
				.Where(software => software.IsDownloading)
				.ToListAsync(cancellationToken);

			if (stale.Count == 0)
			{
				return;
			}

			foreach (var software in stale)
			{
				software.IsDownloading = false;
			}

			await context.SaveChangesAsync(cancellationToken);
			Logger.Instance.Log($"Cleared {stale.Count} stale downloads.", LogLevel.Warning);
		}

		public void ResetIfIdle()
		{
			if (!_gate.Wait(0))
			{
				return;
			}

			try
			{
				var hasActive = _jobsByItem.Values.Any(job => job.IsPendingOrActive || job.IsTaskRunning);
				if (hasActive)
				{
					return;
				}

				_jobsByItem.Clear();
				UpdateOnUiThread(() => ViewModel.Items.Clear());
			}
			finally
			{
				_gate.Release();
			}

			UpdateManagerState();
		}

		public void CancelAll()
		{
			foreach (var job in _jobsByItem.Values)
			{
				job.RequestCancel();
			}

			UpdateManagerState();
		}

		public Task WaitForCompletionAsync()
		{
			var tasks = _jobsByItem.Values
				.Select(job => job.ActiveTask)
				.Where(task => task != null)
				.Cast<Task>()
				.ToArray();

			return tasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
		}

		public void CancelItem(DownloadItemViewModel item)
		{
			if (!_jobsByItem.TryGetValue(item, out var job))
			{
				return;
			}

			job.RequestCancel();
			UpdateManagerState();
		}

		public void MoveItemUp(DownloadItemViewModel item)
		{
			MoveItem(item, -1);
		}

		public void MoveItemDown(DownloadItemViewModel item)
		{
			MoveItem(item, 1);
		}

		public void UpdateDiskStats()
		{
			var stats = LocalSoftwareStatsProvider.GetStats(LocalSoftwarePath);
			ApplyDiskStats(stats);
		}

		public void UpdateDiskStats(LocalSoftwareStats stats)
		{
			ApplyDiskStats(stats);
		}

		private void ApplyDiskStats(LocalSoftwareStats stats)
		{
			_lastStats = stats;
			var diskText = stats.DriveTotalBytes.HasValue && stats.DriveFreeBytes.HasValue
				? $"Disk: {ByteSizeFormatter.FormatBytes(stats.DriveFreeBytes.Value)} free / {ByteSizeFormatter.FormatBytes(stats.DriveTotalBytes.Value)}"
				: "--";
			var folderText = stats.FolderSizeBytes.HasValue
				? $"Local: {ByteSizeFormatter.FormatBytes(stats.FolderSizeBytes.Value)}"
				: "--";

			UpdateOnUiThread(() => ViewModel.UpdateDiskStats(diskText, folderText));
			UpdateTotals();
		}

		private void MoveItem(DownloadItemViewModel item, int offset)
		{
			var index = ViewModel.Items.IndexOf(item);
			if (index < 0)
			{
				return;
			}

			var newIndex = index + offset;
			if (newIndex < 0 || newIndex >= ViewModel.Items.Count)
			{
				return;
			}

			UpdateOnUiThread(() =>
			{
				ViewModel.Items.RemoveAt(index);
				ViewModel.Items.Insert(newIndex, item);
			});
			UpdateMoveStates();
			UpdateManagerState();
			_ = StartQueuedDownloadsAsync(AppLifecycle.Instance.ShutdownToken);
		}

		private bool TryCreateJob(SoftwareRowViewModel selection, out DownloadJob job)
		{
			job = null!;
			if (string.IsNullOrWhiteSpace(selection.WebLink))
			{
				Logger.Instance.Log($"Missing download link for {selection.DisplayName}.", LogLevel.Warning);
				return false;
			}

			if (!DownloadUrlBuilder.TryGetFileName(selection.WebLink, out var fileName, out var error))
			{
				Logger.Instance.Log($"Failed to parse file name for {selection.DisplayName}: {error}", LogLevel.Warning);
				return false;
			}

			if (!DownloadUrlBuilder.TryGetVersionString(selection.WebVersion, selection.WebLink, out var version, out error))
			{
				Logger.Instance.Log($"Failed to parse version for {selection.DisplayName}: {error}", LogLevel.Warning);
				return false;
			}

			var viewModel = new DownloadItemViewModel(selection.DisplayName, version, fileName);
			job = new DownloadJob(selection, viewModel, version, fileName);
			return true;
		}

		private async Task StartQueuedDownloadsAsync(CancellationToken cancellationToken)
		{
			var acquired = false;
			try
			{
				await _gate.WaitAsync(cancellationToken);
				acquired = true;
			}
			catch (OperationCanceledException)
			{
				return;
			}

			try
			{
				var maxConcurrent = GetMaxConcurrentDownloads();
				if (Dispatcher.UIThread.CheckAccess())
				{
					StartQueuedDownloadsCore(maxConcurrent);
					return;
				}

				await Dispatcher.UIThread.InvokeAsync(() => StartQueuedDownloadsCore(maxConcurrent), DispatcherPriority.Background);
			}
			finally
			{
				if (acquired)
				{
					_gate.Release();
				}
			}
		}

		private void StartQueuedDownloadsCore(int maxConcurrent)
		{
			var desired = new List<DownloadJob>();
			var slotsUsed = 0;

			foreach (var item in ViewModel.Items)
			{
				if (!_jobsByItem.TryGetValue(item, out var job))
				{
					continue;
				}

				var status = job.ViewModel.Status;
				if (status == DownloadStatus.Completed ||
					status == DownloadStatus.Warning ||
					status == DownloadStatus.Canceled ||
					status == DownloadStatus.Failed)
				{
					continue;
				}

				if (status == DownloadStatus.CheckingMd5)
				{
					desired.Add(job);
					continue;
				}

				if (slotsUsed >= maxConcurrent)
				{
					continue;
				}

				desired.Add(job);
				slotsUsed++;
			}

			var desiredSet = new HashSet<DownloadJob>(desired);
			foreach (var job in _jobsByItem.Values)
			{
				if (job.ViewModel.Status == DownloadStatus.Downloading && !desiredSet.Contains(job))
				{
					job.RequestPause();
				}
			}

			foreach (var job in desired)
			{
				if (job.ViewModel.Status == DownloadStatus.Queued && !job.IsTaskRunning)
				{
					job.Start();
				}
			}
		}

		private int GetMaxConcurrentDownloads()
		{
			var max = ViewModel.MaxConcurrentDownloads;
			if (!ViewModel.IsParallelEnabled || !ViewModel.CanParallelize)
			{
				return 1;
			}

			return max <= 0 ? 1 : max;
		}

		private async Task DownloadAsync(DownloadJob job, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(LocalSoftwarePath))
			{
				job.SetFailed("Local software folder is not configured.");
				return;
			}

			await UpdateDownloadFlagAsync(job.Source, true, cancellationToken);

			var startTime = DateTime.UtcNow;
			var progressEntry = new LogEntry($"Downloading {job.FileName}", "Elapsed: 0s", level: LogLevel.Task);
			var logHandle = Logger.Instance.Log(progressEntry);
			using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			_ = TimerAsync(startTime, progressEntry, timerCts.Token);
			logHandle.AddDetail("Model", job.Source.DisplayName, LogLevel.Debug);

			try
			{
				var downloadTargets = await BuildDownloadTargetsAsync(job, cancellationToken);
				if (downloadTargets.Count == 0)
				{
					job.SetFailed("No download domains available.");
					logHandle.AddDetail("Error", "No download domains available.", LogLevel.Warning);
					return;
				}

				foreach (var target in downloadTargets)
				{
					job.SetStatus(DownloadStatus.Downloading);
					logHandle.AddDetail("Using mirror", target.ToString(), LogLevel.Info);
					try
					{
						var success = await DownloadFromUriAsync(job, target, cancellationToken);
						if (success)
						{
							logHandle.AddDetail("Download", "Completed", LogLevel.Info);
							await UpdateLocalVersionAfterDownloadAsync(job, cancellationToken);
							return;
						}
						if (job.ViewModel.Status == DownloadStatus.Failed)
						{
							break;
						}
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception ex)
					{
						logHandle.AddDetail("Mirror failed", ex.Message, LogLevel.Warning);
					}
				}

				if (job.ViewModel.Status != DownloadStatus.Canceled && job.ViewModel.Status != DownloadStatus.Failed)
				{
					job.SetFailed("All mirrors failed.");
				}
			}
			catch (OperationCanceledException)
			{
				if (job.PauseRequested)
				{
					job.SetQueued();
					logHandle.AddDetail("Download", "Paused", LogLevel.Info);
				}
				else
				{
					job.SetCanceled();
					logHandle.AddDetail("Download", "Canceled", LogLevel.Warning);
				}
			}
			catch (Exception ex)
			{
				job.SetFailed(ex.Message);
				Logger.Instance.LogException(ex, "Download failed.", LogLevel.Error);
			}
			finally
			{
				timerCts.Cancel();
				var elapsedSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;
				UpdateProgressEntry(progressEntry, elapsedSeconds, ResolveFinalStatus(job.LastStatus));
				await UpdateDownloadFlagAsync(job.Source, false, CancellationToken.None);
				UpdateManagerState();
				await StartQueuedDownloadsAsync(AppLifecycle.Instance.ShutdownToken);
			}
		}

		private async Task<IReadOnlyList<Uri>> BuildDownloadTargetsAsync(DownloadJob job, CancellationToken cancellationToken)
		{
			var domains = await GetRankedDomainsAsync(cancellationToken);
			var targets = new List<Uri>();
			foreach (var domain in domains)
			{
				if (DownloadUrlBuilder.TryBuildDownloadUri(job.Source.WebLink, job.Version, domain, out var uri, out _))
				{
					targets.Add(uri);
				}
			}

			if (Uri.TryCreate(job.Source.WebLink, UriKind.Absolute, out var originalUri))
			{
				var host = originalUri.Host;
				if (DownloadUrlBuilder.TryBuildDownloadUri(job.Source.WebLink, job.Version, host, out var uri, out _)
					&& targets.All(existing => !Uri.Equals(existing, uri)))
				{
					targets.Add(uri);
				}
			}

			return targets;
		}

		private async Task<IReadOnlyList<string>> GetRankedDomainsAsync(CancellationToken cancellationToken)
		{
			await using var context = AppDbContextFactory.Create();
			await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

			var domains = await context.DownloadDomains
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var ranked = domains
				.Where(domain => domain.LastStatus == MirrorStatus.Healthy && domain.LastScore.HasValue)
				.OrderByDescending(domain => domain.LastScore)
				.Select(domain => domain.Domain)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (ranked.Count == 0)
			{
				ranked = domains
					.OrderByDescending(domain => domain.LastScore ?? double.MinValue)
					.Select(domain => domain.Domain)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
			}

			return ranked;
		}

		private async Task<bool> DownloadFromUriAsync(DownloadJob job, Uri downloadUri, CancellationToken cancellationToken)
		{
			job.PreparePaths(LocalSoftwarePath);
			if (string.IsNullOrWhiteSpace(job.TempFilePath) || string.IsNullOrWhiteSpace(job.FinalFilePath))
			{
				job.SetFailed("Download path could not be prepared.");
				return false;
			}

			if (!Directory.Exists(job.ModelFolderPath))
			{
				Directory.CreateDirectory(job.ModelFolderPath!);
			}

			try
			{
				if (File.Exists(job.TempFilePath))
				{
					File.Delete(job.TempFilePath);
				}

				using var response = await _httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					return false;
				}

				var totalBytes = response.Content.Headers.ContentLength ?? 0;
				UpdateProgress(job, 0, totalBytes, 0);

				await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
				await using var fileStream = new FileStream(
					job.TempFilePath,
					FileMode.Create,
					FileAccess.Write,
					FileShare.None,
					bufferSize: 1024 * 256,
					FileOptions.Asynchronous | FileOptions.SequentialScan);

				using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
				var buffer = new byte[1024 * 256];
				var totalRead = 0L;
				var lastReportedBytes = 0L;
				var lastReportAt = DateTimeOffset.UtcNow;
				var startedAt = DateTimeOffset.UtcNow;

				while (true)
				{
					var read = await contentStream.ReadAsync(buffer, cancellationToken);
					if (read <= 0)
					{
						break;
					}

					await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
					hasher.AppendData(buffer.AsSpan(0, read));
					totalRead += read;

					var now = DateTimeOffset.UtcNow;
					if ((now - lastReportAt).TotalMilliseconds >= 500)
					{
						var intervalBytes = totalRead - lastReportedBytes;
						var intervalSeconds = Math.Max((now - lastReportAt).TotalSeconds, 0.01);
						var speed = intervalBytes / intervalSeconds;
						UpdateProgress(job, totalRead, totalBytes, speed);
						lastReportedBytes = totalRead;
						lastReportAt = now;
					}
				}

				var elapsedSeconds = Math.Max((DateTimeOffset.UtcNow - startedAt).TotalSeconds, 0.01);
				UpdateProgress(job, totalRead, totalBytes, totalRead / elapsedSeconds);

				var hash = hasher.GetHashAndReset();
				var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

				UpdateOnUiThread(() => job.ViewModel.UpdateStatus(DownloadStatus.CheckingMd5));
				var md5Prefix = job.Md5Prefix;
				if (!string.IsNullOrWhiteSpace(md5Prefix) &&
					!hashHex.StartsWith(md5Prefix, StringComparison.OrdinalIgnoreCase))
				{
					job.SetWarning($"MD5 mismatch. Expected prefix {md5Prefix}.");
				}
				else if (string.IsNullOrWhiteSpace(md5Prefix))
				{
					job.SetWarning("MD5 prefix not found.");
				}
				else
				{
					job.SetCompleted();
				}

				fileStream.Close();
				try
				{
					File.Move(job.TempFilePath, job.FinalFilePath, true);
				}
				catch (Exception ex)
				{
					job.SetFailed($"Failed to finalize download: {ex.Message}");
					return false;
				}

				return job.ViewModel.Status == DownloadStatus.Completed || job.ViewModel.Status == DownloadStatus.Warning;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch
			{
				job.DeletePartialFile();
				throw;
			}
		}

		private async Task UpdateLocalVersionAfterDownloadAsync(DownloadJob job, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(LocalSoftwarePath))
			{
				return;
			}

			if (!LocalSoftwareScanner.TryGetModelFolderPath(LocalSoftwarePath, job.Source.Name, job.Source.RegionAcronym, out var modelFolderPath))
			{
				modelFolderPath = Path.Combine(LocalSoftwarePath, LocalSoftwareScanner.BuildModelFolderName(job.Source.Name, job.Source.RegionAcronym));
			}

			var latestLocalVersion = LocalSoftwareScanner.GetLatestVersionInModelFolder(modelFolderPath);
			await using var context = AppDbContextFactory.Create();
			await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);
			var softwareRepository = new SoftwareRepository(context);
			var folderRepository = new FolderSourceRepository(context);
			var dbHelper = new DatabaseHelpers(folderRepository, softwareRepository);
			await dbHelper.UpdateLocalVersionAsync(job.Source.Name, job.Source.RegionAcronym, latestLocalVersion);

			UpdateOnUiThread(() => job.Source.UpdateLocalVersion(latestLocalVersion));
		}

		private void UpdateManagerState()
		{
			UpdateOnUiThread(() =>
			{
				var activeCount = ViewModel.Items.Count(item =>
					item.Status == DownloadStatus.Queued ||
					item.Status == DownloadStatus.Downloading ||
					item.Status == DownloadStatus.CheckingMd5);
				var hasActive = activeCount > 0;
				var canParallelize = activeCount > 1;
				ViewModel.SetCanParallelize(canParallelize);

				ViewModel.SetHasActiveDownloads(hasActive);
				UpdateSummary();
				UpdateTotals();
				UpdateAggregateSpeed();
			});
		}

		private void UpdateTotals()
		{
			var totalBytes = ViewModel.Items.Sum(item => item.TotalBytes > 0 ? item.TotalBytes : 0);
			var unknownCount = ViewModel.Items.Count(item => item.TotalBytes <= 0);
			var totalText = ByteSizeFormatter.FormatBytes(totalBytes);
			if (unknownCount > 0)
			{
				totalText = $"{totalText} + {unknownCount} unknown";
			}

			var afterText = "--";
			if (_lastStats.FolderSizeBytes.HasValue)
			{
				var afterBytes = _lastStats.FolderSizeBytes.Value + totalBytes;
				afterText = $"{ByteSizeFormatter.FormatBytes(afterBytes)} (+{ByteSizeFormatter.FormatBytes(totalBytes)})";
			}

			ViewModel.UpdateTotals(totalText, afterText);
		}

		private void UpdateAggregateSpeed()
		{
			var speed = ViewModel.Items
				.Where(item => item.Status == DownloadStatus.Downloading)
				.Sum(item => item.BytesPerSecond);
			ViewModel.UpdateAggregateSpeed(ByteSizeFormatter.FormatBytesPerSecond(speed));
		}

		private void UpdateProgress(DownloadJob job, long downloadedBytes, long totalBytes, double bytesPerSecond)
		{
			UpdateOnUiThread(() =>
			{
				job.ViewModel.UpdateProgress(downloadedBytes, totalBytes, bytesPerSecond);
				UpdateSummary();
				UpdateTotals();
				UpdateAggregateSpeed();
			});
		}

		private void UpdateSummary()
		{
			var activeItem = ViewModel.Items.FirstOrDefault(item =>
				item.Status == DownloadStatus.Downloading || item.Status == DownloadStatus.CheckingMd5);

			if (activeItem == null)
			{
				ViewModel.UpdateSummary(string.Empty);
				return;
			}

			var index = ViewModel.Items.IndexOf(activeItem);
			var total = ViewModel.Items.Count;
			var summary = $"Downloading: ({index + 1}/{total}) {activeItem.SummaryFileName} | {activeItem.SpeedText} | {activeItem.ProgressText}";
			ViewModel.UpdateSummary(summary);
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

		private static string? ResolveFinalStatus(DownloadStatus status)
		{
			return status switch
			{
				DownloadStatus.Completed => "Completed",
				DownloadStatus.Warning => "Warning",
				DownloadStatus.Canceled => "Canceled",
				DownloadStatus.Failed => "Failed",
				DownloadStatus.Queued => "Paused",
				_ => null
			};
		}

		private void UpdateMoveStates()
		{
			UpdateOnUiThread(() =>
			{
				for (var i = 0; i < ViewModel.Items.Count; i++)
				{
					var item = ViewModel.Items[i];
					item.UpdateMoveState(i > 0, i < ViewModel.Items.Count - 1);
				}
			});
		}

		private void AddItem(DownloadItemViewModel item)
		{
			UpdateOnUiThread(() => ViewModel.Items.Add(item));
		}

		private void UpdateOnUiThread(Action action)
		{
			if (Dispatcher.UIThread.CheckAccess())
			{
				action();
				return;
			}

			Dispatcher.UIThread.Post(action);
		}

		private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(DownloadManagerViewModel.MaxConcurrentDownloads) ||
				e.PropertyName == nameof(DownloadManagerViewModel.IsParallelEnabled))
			{
				_ = StartQueuedDownloadsAsync(AppLifecycle.Instance.ShutdownToken);
			}
		}

		private async Task UpdateDownloadFlagAsync(SoftwareRowViewModel source, bool isDownloading, CancellationToken cancellationToken)
		{
			try
			{
				if (source.Software.IsDownloading == isDownloading)
				{
					return;
				}

				source.Software.IsDownloading = isDownloading;
				await using var context = AppDbContextFactory.Create();
				await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

				var software = await context.Software
					.FirstOrDefaultAsync(item => item.Id == source.Software.Id, cancellationToken);
				if (software == null)
				{
					return;
				}

				if (software.IsDownloading == isDownloading)
				{
					return;
				}

				software.IsDownloading = isDownloading;
				await context.SaveChangesAsync(cancellationToken);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Logger.Instance.LogException(ex, "Failed to update download status.", LogLevel.Warning);
			}
		}

		private sealed class DownloadJob
		{
			private bool _pauseRequested;

			public DownloadJob(SoftwareRowViewModel source, DownloadItemViewModel viewModel, string version, string fileName)
			{
				Source = source;
				ViewModel = viewModel;
				Version = version;
				FileName = fileName;
				Md5Prefix = TryGetMd5Prefix(fileName);
				LastStatus = DownloadStatus.Queued;
			}

			public SoftwareRowViewModel Source { get; }

			public DownloadItemViewModel ViewModel { get; }

			public string Version { get; }

			public string FileName { get; }

			public string? Md5Prefix { get; }

			public CancellationTokenSource? CancellationTokenSource { get; private set; }

			public Task? ActiveTask { get; private set; }

			public string? ModelFolderPath { get; private set; }

			public string? FinalFilePath { get; private set; }

			public string? TempFilePath { get; private set; }

			public DownloadStatus LastStatus { get; private set; }

			public bool IsActive => ViewModel.Status == DownloadStatus.Downloading || ViewModel.Status == DownloadStatus.CheckingMd5;

			public bool IsPendingOrActive => ViewModel.Status == DownloadStatus.Queued || IsActive;

			public bool IsTaskRunning => ActiveTask != null && !ActiveTask.IsCompleted;

			public bool PauseRequested => _pauseRequested;

			public void Start()
			{
				_pauseRequested = false;
				CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(AppLifecycle.Instance.ShutdownToken);
				SetStatus(DownloadStatus.Downloading);
				ActiveTask = Task.Run(() => DownloadManagerService.Instance.DownloadAsync(this, CancellationTokenSource.Token), CancellationTokenSource.Token);
			}

			public void RequestCancel()
			{
				_pauseRequested = false;
				try
				{
					if (ViewModel.Status == DownloadStatus.Queued)
					{
						SetCanceled();
						return;
					}

					CancellationTokenSource?.Cancel();
				}
				catch
				{
				}
			}

			public void RequestPause()
			{
				if (ViewModel.Status == DownloadStatus.Queued)
				{
					return;
				}

				_pauseRequested = true;
				try
				{
					CancellationTokenSource?.Cancel();
				}
				catch
				{
				}
			}

			public void PreparePaths(string? localSoftwarePath)
			{
				if (string.IsNullOrWhiteSpace(localSoftwarePath))
				{
					return;
				}

				if (LocalSoftwareScanner.TryGetModelFolderPath(localSoftwarePath, Source.Name, Source.RegionAcronym, out var modelFolderPath))
				{
					ModelFolderPath = modelFolderPath;
				}
				else
				{
					ModelFolderPath = Path.Combine(localSoftwarePath, LocalSoftwareScanner.BuildModelFolderName(Source.Name, Source.RegionAcronym));
				}

				if (string.IsNullOrWhiteSpace(ModelFolderPath))
				{
					return;
				}

				FinalFilePath = Path.Combine(ModelFolderPath, FileName);
				TempFilePath = FinalFilePath + ".partial";
			}

			public void SetFailed(string message)
			{
				LastStatus = DownloadStatus.Failed;
				DownloadManagerService.Instance.UpdateOnUiThread(() => ViewModel.UpdateStatus(DownloadStatus.Failed));
				Logger.Instance.Log($"Download failed: {FileName}", LogLevel.Error, message);
				DeletePartialFile();
			}

			public void SetWarning(string message)
			{
				LastStatus = DownloadStatus.Warning;
				DownloadManagerService.Instance.UpdateOnUiThread(() => ViewModel.UpdateStatus(DownloadStatus.Warning));
				Logger.Instance.Log($"Download warning: {FileName}", LogLevel.Warning, message);
			}

			public void SetCompleted()
			{
				LastStatus = DownloadStatus.Completed;
				DownloadManagerService.Instance.UpdateOnUiThread(() => ViewModel.UpdateStatus(DownloadStatus.Completed));
			}

			public void SetCanceled()
			{
				LastStatus = DownloadStatus.Canceled;
				DownloadManagerService.Instance.UpdateOnUiThread(() => ViewModel.UpdateStatus(DownloadStatus.Canceled));
				DeletePartialFile();
			}

			public void SetQueued()
			{
				LastStatus = DownloadStatus.Queued;
				_pauseRequested = false;
				DownloadManagerService.Instance.UpdateOnUiThread(() => ViewModel.UpdateStatus(DownloadStatus.Queued));
				DeletePartialFile();
			}

			public void SetStatus(DownloadStatus status)
			{
				LastStatus = status;
				DownloadManagerService.Instance.UpdateOnUiThread(() => ViewModel.UpdateStatus(status));
			}

			public void DeletePartialFile()
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(TempFilePath) && File.Exists(TempFilePath))
					{
						File.Delete(TempFilePath);
					}
				}
				catch
				{
				}
			}

			private static string? TryGetMd5Prefix(string fileName)
			{
				var name = Path.GetFileNameWithoutExtension(fileName);
				if (string.IsNullOrWhiteSpace(name))
				{
					return null;
				}

				var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
				var last = parts.Length == 0 ? string.Empty : parts[^1];
				if (string.IsNullOrWhiteSpace(last))
				{
					return null;
				}

				return last.All(IsHexChar) ? last : null;
			}

			private static bool IsHexChar(char c)
			{
				return (c >= '0' && c <= '9') ||
					(c >= 'a' && c <= 'f') ||
					(c >= 'A' && c <= 'F');
			}
		}
	}
}

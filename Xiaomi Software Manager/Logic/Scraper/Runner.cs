using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Data;
using xsm.Data.Repositories;
using xsm.Models;

namespace xsm.Logic.Scraper
{
	public sealed class Runner
	{
		private static readonly Lazy<Runner> LazyInstance = new(() => new Runner());
		private readonly SemaphoreSlim _gate = new(1, 1);
		private Task<IReadOnlyList<ScrapeIssue>>? _activeTask;
		private CancellationTokenSource? _cts;

		public static Runner Instance => LazyInstance.Value;

		public Task<IReadOnlyList<ScrapeIssue>> StartScraperAsync(CancellationToken cancellationToken = default)
		{
			return StartOrReuseScrapeAsync(cancellationToken);
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

		private async Task<IReadOnlyList<ScrapeIssue>> StartOrReuseScrapeAsync(CancellationToken cancellationToken)
		{
			Task<IReadOnlyList<ScrapeIssue>>? task = null;
			await _gate.WaitAsync(cancellationToken);
			try
			{
				if (_activeTask != null && !_activeTask.IsCompleted)
				{
					Logger.Instance.Log("Scraper is already running.", LogLevel.Warning);
					task = _activeTask;
				}
				else
				{
					_cts?.Dispose();
					_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					task = _activeTask = Task.Run(() => RunScraperAsync(_cts.Token), _cts.Token);
				}
			}
			finally
			{
				_gate.Release();
			}

			return task == null ? Array.Empty<ScrapeIssue>() : await task;
		}

		private async Task<IReadOnlyList<ScrapeIssue>> RunScraperAsync(CancellationToken cancellationToken)
		{
			var runLog = Logger.Instance.Log("Scraper run started.");

			try
			{
				var dbPath = DbPath.GetDefaultPath();
				runLog.AddDetail("Database path", dbPath, LogLevel.Debug);

				await using var context = AppDbContextFactory.Create();
				await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

				await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

				try
				{
					var softwareRepository = new SoftwareRepository(context);
					var regionRepository = new RegionRepository(context);
					var folderSourceRepository = new FolderSourceRepository(context);

					var folderSource = await folderSourceRepository.GetByNameAsync(FolderSourceDefaults.LocalSoftwareName);
					if (folderSource == null || string.IsNullOrWhiteSpace(folderSource.Path))
					{
						runLog.AddDetail("Local software folder not configured.", level: LogLevel.Warning);
						await transaction.RollbackAsync(CancellationToken.None);
						return Array.Empty<ScrapeIssue>();
					}
					runLog.AddDetail("Local software folder", folderSource.Path, LogLevel.Debug);

					using var scraper = new Scraper(
						softwareRepository,
						regionRepository,
						folderSourceRepository,
						cancellationToken);

					var issues = await scraper.Scrape();

					await transaction.CommitAsync(cancellationToken);
					runLog.AddDetail("Scraper run finished.");
					return issues;
				}
				catch (OperationCanceledException)
				{
					await transaction.RollbackAsync(CancellationToken.None);
					throw;
				}
				catch
				{
					await transaction.RollbackAsync(CancellationToken.None);
					throw;
				}
			}
			catch (OperationCanceledException)
			{
				runLog.AddDetail("Scraper run canceled.", level: LogLevel.Warning);
				return Array.Empty<ScrapeIssue>();
			}
			catch (Exception ex)
			{
				runLog.AddDetail("Scraper run failed.", ex.Message, LogLevel.Error);
				AddExceptionDetails(runLog, ex);
				return Array.Empty<ScrapeIssue>();
			}
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
	}
}

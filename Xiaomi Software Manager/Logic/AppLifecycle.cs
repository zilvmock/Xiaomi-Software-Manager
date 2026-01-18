using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Logic.Downloads;
using xsm.Logic.Scraper.Selenium;
using xsm.Logic.Mirrors;
using xsm.Logic.Scraper;
using xsm.Models;

namespace xsm.Logic;

public sealed class AppLifecycle
{
	private static readonly Lazy<AppLifecycle> LazyInstance = new(() => new AppLifecycle());
	private readonly CancellationTokenSource _shutdownCts = new();
	private int _shutdownRequested;
	private int _shutdownStarted;

	private AppLifecycle()
	{
	}

	public static AppLifecycle Instance => LazyInstance.Value;

	public CancellationToken ShutdownToken => _shutdownCts.Token;

	public bool IsShutdownRequested => Volatile.Read(ref _shutdownRequested) != 0;

	public void RequestShutdown()
	{
		if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
		{
			return;
		}

		try
		{
			_shutdownCts.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}

		Runner.Instance.RequestStop();
		MirrorRatingRunner.Instance.RequestStop();
		DownloadManagerService.Instance.CancelAll();
	}

	public void PerformEmergencyCleanup()
	{
		RequestShutdown();
		CleanupResources();
	}

	public async Task ShutdownAsync(TimeSpan? timeout = null)
	{
		RequestShutdown();

		if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
		{
			return;
		}

		var tasks = new[]
		{
			AwaitShutdownAsync(Runner.Instance.WaitForCompletionAsync()),
			AwaitShutdownAsync(MirrorRatingRunner.Instance.WaitForCompletionAsync()),
			AwaitShutdownAsync(DownloadManagerService.Instance.WaitForCompletionAsync())
		};

		var waitTask = Task.WhenAll(tasks);
		if (timeout.HasValue)
		{
			var completed = await Task.WhenAny(waitTask, Task.Delay(timeout.Value));
			if (completed != waitTask)
			{
				Logger.Instance.Log("Shutdown timed out waiting for background tasks.", LogLevel.Warning);
			}
			else
			{
				await waitTask;
			}
		}
		else
		{
			await waitTask;
		}

		CleanupResources();
	}

	private static async Task AwaitShutdownAsync(Task task)
	{
		try
		{
			await task;
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, "Shutdown task failed.", LogLevel.Error);
		}
	}

	private static void CleanupResources()
	{
		try
		{
			SqliteConnection.ClearAllPools();
			Logger.Instance.Log("SQLite connection pools cleared.", LogLevel.Debug);
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, "Failed to clear SQLite pools.", LogLevel.Warning);
		}

		DriverProcessCleaner.KillChromeDriverProcesses();
	}
}

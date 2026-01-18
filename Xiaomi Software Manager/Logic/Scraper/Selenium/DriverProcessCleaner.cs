using System;
using System.Diagnostics;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Models;

namespace xsm.Logic.Scraper.Selenium;

internal static class DriverProcessCleaner
{
	public static void KillChromeDriverProcesses(Logger.LogHandle? logHandle = null)
	{
		try
		{
			const string processName = "chromedriver";
			LogDetail(logHandle, "Stopping existing chromedriver processes.", level: LogLevel.Debug);

			var processes = Process.GetProcessesByName(processName);
			foreach (var process in processes)
			{
				process.Kill();
			}

			LogDetail(logHandle,
				processes.Length > 0
					? $"Stopped {processes.Length} chromedriver processes."
					: "No chromedriver processes found.",
				level: LogLevel.Debug);
		}
		catch (Exception ex)
		{
			LogDetail(logHandle, "Failed to stop chromedriver processes.", ex.Message, LogLevel.Warning);
			AddExceptionDetails(logHandle, ex);
		}
	}

	private static void LogDetail(Logger.LogHandle? logHandle, string message, string? description = null,
		LogLevel level = LogLevel.Info)
	{
		if (logHandle != null)
		{
			logHandle.AddDetail(message, description, level);
			return;
		}

		Logger.Instance.Log(message, level, description);
	}

	private static void AddExceptionDetails(Logger.LogHandle? logHandle, Exception exception)
	{
		if (logHandle == null)
		{
			return;
		}

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

using System;
using System.IO;
using System.Threading;
using OpenQA.Selenium.Chrome;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Models;

namespace xsm.Logic.Scraper.Selenium
{
	internal class WebDriverManager : IDisposable
	{
		private readonly string _logDirectory;
		private ChromeDriver? _driver;

		public WebDriverManager(string logDirectory)
		{
			_logDirectory = logDirectory;
		}

		public ChromeDriver GetDriver(Logger.LogHandle? logHandle = null)
		{
			if (_driver != null)
			{
				return _driver;
			}

			LogDetail(logHandle, "Preparing ChromeDriver.");
			DriverProcessCleaner.KillChromeDriverProcesses(logHandle);
			LogDetail(logHandle, "Cooldown", "3s", LogLevel.Debug);
			Thread.Sleep(3000);

			var tries = 0;
			while (tries < 3)
			{
				var attempt = tries + 1;
				LogDetail(logHandle, $"Starting ChromeDriver (attempt {attempt}/3).");
				try
				{
					_driver = SetupChromeDriver(logHandle);
					if (_driver != null)
					{
						LogDetail(logHandle, "ChromeDriver ready.");
						break;
					}

					LogDetail(logHandle, "ChromeDriver returned null.", level: LogLevel.Warning);
				}
				catch (Exception ex)
				{
					LogDetail(logHandle, "ChromeDriver start failed.", ex.Message, LogLevel.Error);
					AddExceptionDetails(logHandle, ex);
				}

				tries++;
				if (_driver == null)
				{
					LogDetail(logHandle, "Retrying...", level: LogLevel.Warning);
					Thread.Sleep(1000);
				}
			}

			return _driver ?? throw new ArgumentException("Cannot start the web driver.");
		}

		private ChromeDriver? SetupChromeDriver(Logger.LogHandle? logHandle)
		{
			try
			{
				Directory.CreateDirectory(_logDirectory);
				var driverPath = Path.Combine(ChromeDriverInstaller.DriverDirectory,
					ChromeDriverInstaller.DriverExecutableName);
				var hadDriver = File.Exists(driverPath);
				LogDetail(logHandle, "Driver path", driverPath, LogLevel.Debug);

				if (!hadDriver)
				{
					LogDetail(logHandle, "ChromeDriver not found locally. Downloading...", level: LogLevel.Warning);
				}

				ChromeDriverInstaller.EnsureDriver();
				if (!hadDriver)
				{
					LogDetail(logHandle, "ChromeDriver downloaded.");
				}

				var options = ScraperConfig.ChromeSettings.GetChromeOptions();
				var driverService = ScraperConfig.ChromeSettings.GetDriverService(
					_logDirectory,
					ChromeDriverInstaller.DriverDirectory,
					ChromeDriverInstaller.DriverExecutableName);

				LogDetail(logHandle, "Driver log", driverService.LogPath, LogLevel.Debug);

				return new ChromeDriver(driverService, options);
			}
			catch (Exception ex)
			{
				LogDetail(logHandle, "Setup failed.", ex.Message, LogLevel.Error);
				AddExceptionDetails(logHandle, ex);
				return null;
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

		public void Dispose()
		{
			if (_driver == null) return;

			_driver.Quit();
			_driver.Dispose();
			_driver = null;
		}
	}
}

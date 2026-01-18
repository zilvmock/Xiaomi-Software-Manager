using System;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace xsm.Logic.Scraper
{
	public static class ScraperConfig
	{
		public const string XiaomiFirmwareUrl = "http://new.c.mi.com/global/miuidownload/detail/guide/2";
		public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
		public static readonly TimeSpan PageLoadTimeout = TimeSpan.FromSeconds(30);
		public static readonly TimeSpan ElementWaitTimeout = TimeSpan.FromSeconds(10);

		public static class ChromeSettings
		{
			public static ChromeOptions GetChromeOptions()
			{
				var options = new ChromeOptions();
				options.AddArgument("--log-level=3");              // Set to FATAL only
				options.AddArgument("--disable-logging");          // Disable general logging
				options.AddArgument("--disable-extensions");       // Disable extensions
				options.AddArgument("--disable-popup-blocking");   // Disable popup blocking
				options.AddArgument("--disable-notifications");    // Disable notifications
				options.AddArgument("--disable-gpu");              // Disable GPU
				options.AddArgument("--headless");                 // Run in headless mode

				options.SetLoggingPreference(LogType.Browser, LogLevel.Severe);
				options.SetLoggingPreference(LogType.Driver, LogLevel.Severe);
				options.SetLoggingPreference(LogType.Performance, LogLevel.Off);

				return options;
			}

			public static ChromeDriverService GetDriverService(
				string logDirectory,
				string driverDirectory,
				string driverExecutableName)
			{
				var driverService = ChromeDriverService.CreateDefaultService(
					driverDirectory,
					driverExecutableName);
				driverService.EnableVerboseLogging = false;
				driverService.HideCommandPromptWindow = true;
				driverService.LogPath = Path.Combine(logDirectory, $"chromedriver_{DateTime.Now:yyyyMMdd_HHmmss}.log");

				return driverService;
			}
		}
	}
}

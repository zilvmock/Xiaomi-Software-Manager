using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace xsm.Logic.Scraper.Selenium
{
	internal static class ChromeDriverInstaller
	{
		private const string MetadataUrl =
			"https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions-with-downloads.json";
		private const string VersionOverrideEnv = "XSM_CHROMEDRIVER_VERSION";

		public static string DriverDirectory => Path.Combine(AppContext.BaseDirectory, "drivers");
		public static string DriverExecutableName => OperatingSystem.IsWindows() ? "chromedriver.exe" : "chromedriver";

		public static string EnsureDriver()
		{
			var driverPath = Path.Combine(DriverDirectory, DriverExecutableName);
			if (File.Exists(driverPath))
			{
				return driverPath;
			}

			Directory.CreateDirectory(DriverDirectory);

			try
			{
				DownloadDriver(driverPath);
				return driverPath;
			}
			catch
			{
				var bundledPath = Path.Combine(AppContext.BaseDirectory, DriverExecutableName);
				if (File.Exists(bundledPath) && !string.Equals(bundledPath, driverPath, StringComparison.OrdinalIgnoreCase))
				{
					File.Copy(bundledPath, driverPath, overwrite: true);
					EnsureExecutable(driverPath);
					return driverPath;
				}

				throw;
			}
		}

		private static void DownloadDriver(string driverPath)
		{
			var platform = GetPlatform();
			var downloadUrl = GetDownloadUrl(platform);

			var tempRoot = Path.Combine(DriverDirectory, "chromedriver_tmp");
			if (Directory.Exists(tempRoot))
			{
				Directory.Delete(tempRoot, true);
			}
			Directory.CreateDirectory(tempRoot);

			var zipPath = Path.Combine(tempRoot, "chromedriver.zip");
			using (var http = new HttpClient())
			using (var response = http.GetAsync(downloadUrl).GetAwaiter().GetResult())
			{
				response.EnsureSuccessStatusCode();
				using var stream = response.Content.ReadAsStream();
				using var file = File.Create(zipPath);
				stream.CopyTo(file);
			}

			ZipFile.ExtractToDirectory(zipPath, tempRoot);

			var extractedPath = Directory.GetFiles(tempRoot, DriverExecutableName, SearchOption.AllDirectories)
				.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(extractedPath))
			{
				throw new FileNotFoundException("Downloaded ChromeDriver archive did not contain a driver binary.");
			}

			File.Copy(extractedPath, driverPath, true);
			EnsureExecutable(driverPath);

			Directory.Delete(tempRoot, true);
		}

		private static string GetDownloadUrl(string platform)
		{
			var versionOverride = Environment.GetEnvironmentVariable(VersionOverrideEnv);
			if (!string.IsNullOrWhiteSpace(versionOverride))
			{
				return BuildDownloadUrl(versionOverride.Trim(), platform);
			}

			using var http = new HttpClient();
			var json = http.GetStringAsync(MetadataUrl).GetAwaiter().GetResult();
			using var doc = JsonDocument.Parse(json);

			var downloads = doc.RootElement
				.GetProperty("channels")
				.GetProperty("Stable")
				.GetProperty("downloads")
				.GetProperty("chromedriver");

			foreach (var entry in downloads.EnumerateArray())
			{
				var entryPlatform = entry.GetProperty("platform").GetString();
				if (string.Equals(entryPlatform, platform, StringComparison.OrdinalIgnoreCase))
				{
					var url = entry.GetProperty("url").GetString();
					if (string.IsNullOrWhiteSpace(url))
					{
						throw new InvalidOperationException("ChromeDriver download URL missing in metadata.");
					}
					return url;
				}
			}

			throw new InvalidOperationException($"No ChromeDriver download available for platform '{platform}'.");
		}

		private static string BuildDownloadUrl(string version, string platform)
			=> $"https://storage.googleapis.com/chrome-for-testing-public/{version}/{platform}/chromedriver-{platform}.zip";

		private static string GetPlatform()
		{
			if (OperatingSystem.IsWindows())
			{
				return RuntimeInformation.OSArchitecture == Architecture.X86 ? "win32" : "win64";
			}

			if (OperatingSystem.IsMacOS())
			{
				return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "mac-arm64" : "mac-x64";
			}

			return "linux64";
		}

		private static void EnsureExecutable(string driverPath)
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}

			var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
			           UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
			           UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
			File.SetUnixFileMode(driverPath, mode);
		}
	}
}

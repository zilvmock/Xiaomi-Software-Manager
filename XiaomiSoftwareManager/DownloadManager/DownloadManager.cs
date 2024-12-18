using System.Globalization;
using System.IO;
using System.Net.Http;

namespace XiaomiSoftwareManager.DownloadManager
{
	public static class BufferSizes
	{
		public static int KB(int kb) => kb * 1024;
		public static int MB(int mb) => mb * KB(1024);
	}

	public class DownloadManager
	{
		public event Action<string>? DownloadSpeedChanged;
		public event Action<string>? DownloadSizeChanged;
		public event Action<int>? DownloadPercentChanged;
		public event Action? DownloadStarted;

		private readonly string downloadFolder = Directory.GetCurrentDirectory();

		/// <summary>
		/// Downloads a file from the provided URL.
		/// </summary>
		/// <returns>File path.</returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="IOException"></exception>
		/// <exception cref="TimeoutException"></exception>
		public async Task<string> DownloadFileAsync(
			string url,
			string fileName = "default: From The URL",
			string downloadPath = "default: Application Directory",
			int bufferSize = 65536,
			HttpClient client = null!,
			TimeSpan? downloadTimeout = null,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			downloadTimeout ??= TimeSpan.FromMinutes(10);
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(downloadTimeout.Value);

			try
			{
				void ResetTimeout() => cts.CancelAfter(downloadTimeout.Value);

				using (client ??= new HttpClient())
				{
					if (fileName.Contains("default"))
					{
						fileName = await GetFileNameFromUrlAsync(url);
						if (string.IsNullOrEmpty(fileName)) { throw new InvalidOperationException("Cannot get a file extension from the response."); }
					}
					if (downloadPath.Contains("default")) { downloadPath = downloadFolder; }
					else { if (!Directory.Exists(downloadPath)) { throw new InvalidOperationException("Download path does not exist."); } }

					string filePath = Path.Combine(downloadPath, fileName);

					using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
					response.EnsureSuccessStatusCode();
					cancellationToken.ThrowIfCancellationRequested();

					long totalBytes = response.Content.Headers.ContentLength ?? 0;
					long downloadedBytes = 0;
					byte[] buffer = new byte[bufferSize];
					int bytesRead;
					DateTime startTime = DateTime.MinValue;

					using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
					using var fileStream = new FileStream(
						filePath,
						FileMode.Create,
						FileAccess.Write,
						FileShare.None,
						bufferSize,
						FileOptions.Asynchronous
					);

					startTime = DateTime.Now;
					DownloadStarted?.Invoke();

					while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
					{
						cancellationToken.ThrowIfCancellationRequested();
						await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
						downloadedBytes += bytesRead;

						int progress = (int)((double)downloadedBytes / totalBytes * 100);
						DownloadSpeedChanged?.Invoke($"{FormatSpeed(downloadedBytes, startTime)}");
						DownloadSizeChanged?.Invoke($"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}");
						DownloadPercentChanged?.Invoke(progress);
						ResetTimeout();
					}

					DownloadSpeedChanged?.Invoke("-");
					return filePath;
				}
			}
			catch (HttpRequestException ex)
			{
				throw new IOException($"Network error downloading file: {ex.Message}", ex);
			}
			catch (TaskCanceledException ex)
			{
				throw new TimeoutException("Download operation timed out", ex);
			}
			finally
			{
				client?.Dispose();
			}
		}

		private static readonly Dictionary<string, string> MimeTypeToExtensionMap = new()
		{
			{ "application/octet-stream", ".bin" },
			{ "application/x-msdownload", ".exe" },
			{ "application/x-zip-compressed", ".zip" },
			{ "application/zip", ".zip" },
			{ "application/gzip", ".gz" },
			{ "application/x-tar", ".tar" },
			{ "application/x-rar-compressed", ".rar" },
		};

		/// <returns>File name with extension.</returns>
		public static async Task<string> GetFileNameFromUrlAsync(string url, HttpClient client = null!)
		{
			using (client ??= new HttpClient())
			{
				using HttpRequestMessage request = new(HttpMethod.Head, url);
				using HttpResponseMessage response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();

				if (response.Content.Headers.ContentDisposition != null)
				{
					string? fileName = response.Content.Headers.ContentDisposition.FileName?.Trim('"');
					if (!string.IsNullOrEmpty(fileName))
					{
						return fileName;
					}
				}

				if (response.Content.Headers.ContentType != null)
				{
					string? mimeType = response.Content.Headers.ContentType.MediaType;
					if (!string.IsNullOrEmpty(mimeType))
					{
						if (MimeTypeToExtensionMap.TryGetValue(mimeType, out string? extension)) { return $"downloaded-file{extension}"; }
					}
				}
			}

			return string.Empty;
		}

		private static string FormatBytes(long bytes)
		{
			var culture = CultureInfo.InvariantCulture;

			if (bytes < 1024) return $"{bytes} B";
			else if (bytes < 1048576) return $"{(bytes / 1024.0).ToString("F2", culture)} KB";
			else return $"{(bytes / 1048576.0).ToString("F2", culture)} MB";
		}

		private static string FormatBytesPerSecond(double speed)
		{
			var culture = CultureInfo.InvariantCulture;

			if (speed < 1024)
				return $"{speed.ToString("F2", culture)} B/s";
			else if (speed < 1048576)
				return $"{(speed / 1024.0).ToString("F2", culture)} KB/s";
			else if (speed < 1073741824)
				return $"{(speed / 1048576.0).ToString("F2", culture)} MB/s";
			else
				return $"{(speed / 1073741824.0).ToString("F2", culture)} GB/s";
		}

		private static string FormatSpeed(long downloadedBytes, DateTime startTime)
		{
			double elapsedTime = (DateTime.Now - startTime).TotalSeconds;
			double speed = downloadedBytes / elapsedTime;
			return FormatBytesPerSecond(speed);
		}
	}
}

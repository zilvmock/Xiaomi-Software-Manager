using Moq;
using Moq.Protected;
using System.Net;
using System.Reflection;
using Tests.Helpers;
using XiaomiSoftwareManager.DownloadManager;

namespace Tests
{
	/* 
	 * NOTES:
	 * For "DownloadFileAsync" tests send "fileName" and "downloadPath" to skip the call of "GetFileNameFromUrlAsync",
	 * because it checks for valid URL and fails the test. "GetFileNameFromUrlAsync" is tested separetely.
	*/
	public class DownloadManagerTests
	{
		private static HttpClient CreateMockHttpClient(string url, string content, HttpMethod type, HttpStatusCode statusCode = HttpStatusCode.OK)
		{
			var mockHandler = new Mock<HttpMessageHandler>();

			mockHandler
				.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.Is<HttpRequestMessage>(req =>
						req.Method == type && req.RequestUri!.ToString() == url),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = statusCode,
					Content = new StringContent(content)
				});

			return new HttpClient(mockHandler.Object);
		}

		[Fact]
		public async Task DownloadFileAsync_ShouldDownloadFileSuccessfully()
		{
			// Arrange
			string url = "https://nonexistingurl123.com/testfile.txt";
			string expectedFileName = "testfile.txt";
			string expectedContent = "Sample file content";
			TempDirectoryManager tempDirectoryManager = new();

			var httpClient = CreateMockHttpClient(url, expectedContent, type: HttpMethod.Get);

			DownloadManager downloadManager = new();

			// Act
			string filePath = await downloadManager.DownloadFileAsync(
				url,
				fileName: expectedFileName,
				downloadPath: tempDirectoryManager.TempDirectory,
				client: httpClient
			);

			// Assert
			Assert.True(File.Exists(filePath));
			Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath));
		}

		[Fact]
		public async Task DownloadFileAsync_ShouldRespectCancellationToken()
		{
			// Arrange
			string url = "https://example.com/testfile.txt";
			var httpClient = CreateMockHttpClient(url, "Sample file content", type: HttpMethod.Get);
			using var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.Cancel();
			DownloadManager downloadManager = new();

			// Act & Assert
			await Assert.ThrowsAsync<OperationCanceledException>(async () =>
				await downloadManager.DownloadFileAsync(
					url,
					cancellationToken: cancellationTokenSource.Token,
					client: httpClient
				)
			);
		}

		[Fact]
		public async Task DownloadFileAsync_ShouldInvokeProgressEvents()
		{
			// Arrange
			string url = "https://nonexistingurl123.com/testfile.txt";
			TempDirectoryManager tempDirectoryManager = new();
			DownloadManager downloadManager = new();
			var httpClient = CreateMockHttpClient(url, "Test", type: HttpMethod.Get);

			List<string> speedEvents = [];
			List<string> sizeEvents = [];
			List<int> percentEvents = [];
			bool startedEventCalled = false;
			void changeBool() => startedEventCalled = true;

			downloadManager.DownloadSpeedChanged += speedEvents.Add;
			downloadManager.DownloadSizeChanged += sizeEvents.Add;
			downloadManager.DownloadPercentChanged += percentEvents.Add;
			downloadManager.DownloadStarted += changeBool;

			// Act
			await downloadManager.DownloadFileAsync(
				url,
				fileName: "test.txt",
				downloadPath: tempDirectoryManager.TempDirectory,
				client: httpClient
			);

			// Assert
			Assert.NotEmpty(speedEvents);
			Assert.NotEmpty(sizeEvents);
			Assert.NotEmpty(percentEvents);
			Assert.True(startedEventCalled);
		}

		[Fact]
		public async Task DownloadFileAsync_ShouldThrowIOException_InvalidUrl()
		{
			// Arrange
			string url = "https://nonexistingurl123.com/testfile.txt";
			var httpClient = CreateMockHttpClient(url, "", type: HttpMethod.Get, statusCode: HttpStatusCode.NotFound);
			DownloadManager downloadManager = new();

			// Act & Assert
			await Assert.ThrowsAsync<IOException>(async () =>
				await downloadManager.DownloadFileAsync(url, client: httpClient)
			);
		}

		[Fact]
		public async Task DownloadFileAsync_ShouldThrowInvalidOperationException_InvalidDownloadPath()
		{
			// Arrange
			string url = "https://nonexistingurl123.com/testfile.txt";
			var httpClient = CreateMockHttpClient(url, "", type: HttpMethod.Get, statusCode: HttpStatusCode.NotFound);
			DownloadManager downloadManager = new();

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(async () =>
				await downloadManager.DownloadFileAsync(
					url,
					fileName: "test.txt",
					downloadPath: "",
					client: httpClient)
			);
		}

		[Fact]
		public async Task DownloadFileAsync_ShouldThrowTimeoutException_DownloadExceedsTimeout()
		{
			// Arrange
			TempDirectoryManager tempDirectoryManager = new();
			var url = "https://example.com/largefile.zip";
			var timeout = TimeSpan.FromSeconds(1);

			var mockHandler = new Mock<HttpMessageHandler>();

			mockHandler
				.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
				.Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
				{
					await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
					return new HttpResponseMessage(HttpStatusCode.OK);
				});

			// Act & Assert
			var client = new HttpClient(mockHandler.Object);
			var downloadManager = new DownloadManager();

			// Act: Call DownloadFileAsync and assert that TimeoutException is thrown
			await Assert.ThrowsAsync<TimeoutException>(async () =>
			{
				await downloadManager.DownloadFileAsync(
					url,
					fileName: "test.txt",
					downloadPath: tempDirectoryManager.TempDirectory,
					downloadTimeout: timeout,
					client: client
				);
			});
		}

		[Fact]
		public async Task GetFileNameFromUrlAsync_GetsFileNameCorrectly_FromHeader()
		{
			// Arrange
			string url = "https://github.com/zilvmock/Xiaomi-Software-Manager/releases/download/v0.1.0/xiaomi-software-manager-0.1.0.zip";
			var mockHandler = new Mock<HttpMessageHandler>();

			mockHandler
				.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.Is<HttpRequestMessage>(req =>
						req.Method == HttpMethod.Head && req.RequestUri!.ToString() == url),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StringContent("test")
					{
						Headers =
						{
							ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
							{
								FileName = "test.txt"
							}
						}
					}
				});

			// Act
			string result = await DownloadManager.GetFileNameFromUrlAsync(url, new HttpClient(mockHandler.Object));

			// Assert
			Assert.False(string.IsNullOrEmpty(result));
		}

		[Fact]
		public async Task GetFileNameFromUrlAsync_GetsFileNameCorrectly_FromMimeType()
		{
			// Arrange
			string url = "https://nonexistingurl123.com/testfile.txt";
			var mockHandler = new Mock<HttpMessageHandler>();

			mockHandler
				.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.Is<HttpRequestMessage>(req =>
						req.Method == HttpMethod.Head && req.RequestUri!.ToString() == url),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StringContent("test")
					{
						Headers =
						{
							ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip")
						}
					}
				});

			// Act
			string result = await DownloadManager.GetFileNameFromUrlAsync(url, new HttpClient(mockHandler.Object));

			// Assert
			Assert.False(string.IsNullOrEmpty(result));
		}

		[Fact]
		public async Task GetFileNameFromUrlAsync_FailsToGetFileName_BadResponseHeader()
		{
			// Arrange
			string url = "https://nonexistingurl123.com/testfile.txt";
			var httpClient = CreateMockHttpClient(url, "Sample file content", type: HttpMethod.Head);

			// Act
			string result = await DownloadManager.GetFileNameFromUrlAsync(url, httpClient);

			// Assert
			Assert.True(string.IsNullOrEmpty(result));
		}


		[Fact]
		public void FormatBytes_ReturnsCorrectFormat()
		{
			// Arrange
			var updater = new Updater();
			var privateMethod = typeof(DownloadManager).GetMethod("FormatBytes", BindingFlags.NonPublic | BindingFlags.Static)!;

			// Act & Assert
			Assert.NotNull(privateMethod);
			Assert.Equal("500 B", privateMethod.Invoke(updater, [500L]));
			Assert.Equal("1.00 KB", privateMethod.Invoke(updater, [1024L]));
			Assert.Equal("1.50 MB", privateMethod.Invoke(updater, [1572864L]));
		}

		[Fact]
		public void FormatBytesPerSecond_ReturnsCorrectFormat()
		{
			// Arrange
			var updater = new DownloadManager();
			var privateMethod = typeof(DownloadManager).GetMethod("FormatBytesPerSecond", BindingFlags.NonPublic | BindingFlags.Static)!;

			// Act & Assert
			Assert.NotNull(updater);
			Assert.NotNull(privateMethod);
			Assert.Equal("500.00 B/s", privateMethod.Invoke(updater, [500.0]));
			Assert.Equal("1.00 KB/s", privateMethod.Invoke(updater, [1024.0]));
			Assert.Equal("1.50 MB/s", privateMethod.Invoke(updater, [1572864.0]));
			Assert.Equal("1.50 GB/s", privateMethod.Invoke(updater, [1610612736.0]));
		}
	}
}

using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using System.Net;
using System.Reflection;
using Tests.Helpers;
using XiaomiSoftwareManager.DownloadManager;
using XiaomiSoftwareManager.Models;

namespace Tests.UpdaterTests
{
	public class UpdaterTests
	{
		[Fact]
		public async Task GetLatestReleaseAsync_ReturnsLatestRelease()
		{
			// Arrange
			var mockReleases = new List<GitHubRelease>
			{
				new GitHubRelease
				{
					TagName = "v1.0.0",
					CreatedAt = DateTime.Now.AddDays(-1),
					Prerelease = false,
					Assets = new List<GitHubAsset>
					{
						new GitHubAsset
						{
							Name = "XiaomiSoftwareManager.zip",
							Size = 1024000,
							ContentType = "application/zip",
							BrowserDownloadUrl = "http://example.com/XiaomiSoftwareManager.zip"
						}
					}
				},
				new GitHubRelease
				{
					TagName = "v0.9.5",
					CreatedAt = DateTime.Now.AddDays(-5),
					Prerelease = false,
					Assets = new List<GitHubAsset>()
				}
			};

			var mockHandler = new Mock<HttpMessageHandler>();
			mockHandler.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.IsAny<HttpRequestMessage>(),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StringContent(JsonConvert.SerializeObject(mockReleases))
				});

			using var mockHttpClient = new HttpClient(mockHandler.Object);

			var updater = new Updater();

			// Act
			var result = await updater.GetLatestReleaseAsync(mockHttpClient);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("v1.0.0", result.TagName);
			Assert.False(result.Prerelease);
			Assert.Single(result.Assets);
			Assert.Equal("XiaomiSoftwareManager.zip", result.Assets.First().Name);
		}

		[Fact]
		public async Task GetLatestReleaseAsync_PreRelease_ReturnsLatestPreRelease()
		{
			// Arrange
			var mockReleases = new List<GitHubRelease>
			{
				new GitHubRelease
				{
					TagName = "v1.0.0",
					CreatedAt = DateTime.Now.AddDays(-1),
					Prerelease = false,
					Assets = new List<GitHubAsset>
					{
						new GitHubAsset
						{
							Name = "XiaomiSoftwareManager.zip",
							Size = 1024000,
							ContentType = "application/zip",
							BrowserDownloadUrl = "http://example.com/XiaomiSoftwareManager.zip"
						}
					}
				},
				new GitHubRelease
				{
					TagName = "v0.9.5-beta",
					CreatedAt = DateTime.Now.AddDays(-5),
					Prerelease = true,
					Assets = new List<GitHubAsset>
					{
						new GitHubAsset
						{
							Name = "XiaomiSoftwareManager.zip",
							Size = 1024000,
							ContentType = "application/zip",
							BrowserDownloadUrl = "http://example.com/XiaomiSoftwareManager.zip"
						}
					}
				}
			};

			var mockHandler = new Mock<HttpMessageHandler>();
			mockHandler.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.IsAny<HttpRequestMessage>(),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StringContent(JsonConvert.SerializeObject(mockReleases))
				});

			using var mockHttpClient = new HttpClient(mockHandler.Object);

			var updater = new Updater();

			// Act
			var result = await updater.GetLatestReleaseAsync(mockHttpClient, true);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("v0.9.5-beta", result.TagName);
			Assert.True(result.Prerelease);
			Assert.Single(result.Assets);
			Assert.Equal("XiaomiSoftwareManager.zip", result.Assets.First().Name);
		}

		[Theory]
		[InlineData("v1.0.0", "v1.0.1", true)]
		[InlineData("v1.0.0", "v1.1.0", true)]
		[InlineData("v1.0.0", "v2.0.0", true)]
		[InlineData("v1.0.0", "v1.0.0", false)]
		[InlineData("v1.0.0", "v0.9.9", false)]
		[InlineData("v1.0.0-alpha", "v1.0.0", true)]
		[InlineData("v1.0.0", "v1.0.0-beta", false)]
		[InlineData("v1.0.0-alpha", "v1.0.0-beta", true)]
		public void UpdateIsAvailable_VersionComparison(string currentVersion, string latestVersion, bool expectedResult)
		{
			// Arrange
			var updater = new Updater();

			// Act
			var result = Updater.UpdateIsAvailable(currentVersion, latestVersion);

			// Assert
			Assert.Equal(expectedResult, result);
		}

		[Fact]
		public void UpdateIsAvailable_InvalidVersionFormat_ReturnsFalse()
		{
			// Act & Assert
			Assert.False(Updater.UpdateIsAvailable("1.0.0", "v1.0.1"));     // Missing 'v'
			Assert.False(Updater.UpdateIsAvailable("v1.0", "v1.0.1"));      // Incomplete version
			Assert.False(Updater.UpdateIsAvailable("v1.0.0.1", "v1.0.1"));  // Too many version parts
		}

		[Fact]
		public async Task DownloadUpdateAsync_UIEventsTrigger()
		{
			// Arrange
			var mockRelease = new GitHubRelease
			{
				TagName = "v1.0.0",
				CreatedAt = DateTime.Now.AddDays(-1),
				Prerelease = false,
				Assets = new List<GitHubAsset>
		{
			new GitHubAsset
			{
				Name = "TestApplication.zip",
				Size = 1024 * 1024, // 1 MB
				ContentType = "application/zip",
				BrowserDownloadUrl = "http://example.com/TestApplication.zip"
			}
		}
			};

			var mockHandler = new Mock<HttpMessageHandler>();
			mockHandler.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == mockRelease.Assets[0].BrowserDownloadUrl),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StreamContent(new MemoryStream(new byte[1024 * 512])) // 512 KB content
				});
			var httpClient = new HttpClient(mockHandler.Object);

			var updater = new Updater();
			var downloadFolderField = typeof(Updater).GetField("downloadFolder", BindingFlags.NonPublic | BindingFlags.Instance);
			if (downloadFolderField == null) { throw new InvalidOperationException("The 'downloadFolder' field could not be found."); }
			using var tempDirectoryManager = new TempDirectoryManager();
			downloadFolderField.SetValue(updater, tempDirectoryManager.TempDirectory);

			// Event tracking variables
			bool downloadStartedCalled = false;
			string lastDownloadSpeed = string.Empty;
			string lastDownloadSize = string.Empty;
			int lastDownloadPercent = -1;

			// Setup event handlers
			updater.DownloadStarted += () => downloadStartedCalled = true;
			updater.DownloadSpeedChanged += speed => lastDownloadSpeed = speed;
			updater.DownloadSizeChanged += size => lastDownloadSize = size;
			updater.DownloadPercentChanged += percent => lastDownloadPercent = percent;

			// Act
			string downloadedFilePath = await updater.DownloadUpdateAsync(mockRelease, httpClient);

			// Assert
			Assert.True(downloadStartedCalled, "DownloadStarted event should have been fired");
			Assert.NotEqual(string.Empty, lastDownloadSpeed);
			Assert.NotEqual(string.Empty, lastDownloadSize);
			Assert.True(lastDownloadPercent > 0 && lastDownloadPercent <= 100, "Download percent should have been updated between 0 and 100");
			Assert.False(string.IsNullOrEmpty(downloadedFilePath), "Downloaded file path should not be null or empty");
			Assert.True(File.Exists(downloadedFilePath), "The downloaded file should exist");
		}

		[Fact]
		public async Task DownloadUpdateAsync_DownloadsCorrectZip()
		{
			// Arrange
			var mockRelease = new GitHubRelease
			{
				TagName = "v1.0.0",
				CreatedAt = DateTime.Now.AddDays(-1),
				Prerelease = false,
				Assets = new List<GitHubAsset>
				{
					new GitHubAsset
					{
						Name = "XiaomiSoftwareManager.zip",
						Size = 1024, // 1 KB
						ContentType = "application/zip",
						BrowserDownloadUrl = "http://example.com/XiaomiSoftwareManager.zip"
					},
					new GitHubAsset
					{
						Name = "XiaomiSoftwareManager2.zip",
						Size = 1024, // 1 KB
						ContentType = "application/x-zip-compressed",
						BrowserDownloadUrl = "http://example.com/XiaomiSoftwareManager2.zip"
					}
				}
			};

			var mockHandler = new Mock<HttpMessageHandler>();
			mockHandler.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == mockRelease.Assets[0].BrowserDownloadUrl),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StreamContent(new MemoryStream(new byte[1024])) // Simulate a 1 KB file
				});

			var httpClient = new HttpClient(mockHandler.Object);
			var updater = new Updater();
			var downloadFolderField = typeof(Updater).GetField("downloadFolder", BindingFlags.NonPublic | BindingFlags.Instance);

			if (downloadFolderField == null) { throw new InvalidOperationException("The 'downloadFolder' field could not be found."); }
			using var tempDirectoryManager = new TempDirectoryManager();
			downloadFolderField.SetValue(updater, tempDirectoryManager.TempDirectory);

			// Act
			string downloadedFilePath = await updater.DownloadUpdateAsync(mockRelease, httpClient, "");

			// Assert
			Assert.False(string.IsNullOrEmpty(downloadedFilePath), "Downloaded file path should not be null or empty.");
			Assert.True(File.Exists(downloadedFilePath), "The downloaded file should exist.");
			Assert.Equal("XiaomiSoftwareManager.zip", Path.GetFileName(downloadedFilePath));
		}

		[Fact]
		public void InstallUpdate_MissingUpdaterExe_ThrowsFileNotFoundException()
		{
			// Arrange
			var updater = new Updater();
			using var tempDirectoryManager = new TempDirectoryManager();
			string zipFilePath = tempDirectoryManager.CreateTestZipFile();
			var downloadFolderField = typeof(Updater).GetField("downloadFolder", BindingFlags.NonPublic | BindingFlags.Instance);
			if (downloadFolderField == null) { throw new InvalidOperationException("The 'downloadFolder' field could not be found."); }
			downloadFolderField.SetValue(updater, tempDirectoryManager.TempDirectory);

			// Act & Assert
			Assert.Throws<FileNotFoundException>(() => updater.InstallUpdate(zipFilePath));
		}
	}
}
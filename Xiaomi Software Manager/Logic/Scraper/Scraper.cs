using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using OpenQA.Selenium;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Data.Interfaces;
using xsm.Data.Services;
using xsm.Logic.Helpers;
using xsm.Logic.Scraper.Parsing;
using xsm.Logic.Scraper.Selenium;
using xsm.Models;
using AppLogLevel = xsm.Models.LogLevel;
using LogEntry = xsm.Models.LogEntry;

namespace xsm.Logic.Scraper
{
	internal class Scraper : IDisposable
	{
		public event Action? ScrapeStart;
		public event Action? ScrapeEnd;

		public List<ScrapeIssue> IrregularSoftware { get; } = new();

		private readonly CancellationToken _cancellationToken;
		private readonly CancellationTokenSource _timerCts = new();
		private readonly string _logDirectory;
		private readonly ISoftwareRepository _softwareRepository;
		private readonly IRegionRepository _regionRepository;
		private readonly IFolderSourceRepository _folderSourceRepository;
		private readonly SoftwareService _softwareService;
		private readonly WebDriverManager _driverManager;
		private readonly RegionResolver _regionResolver;

		public Scraper(
			ISoftwareRepository softwareRepository,
			IRegionRepository regionRepository,
			IFolderSourceRepository folderSourceRepository,
			CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
			_softwareRepository = softwareRepository;
			_regionRepository = regionRepository;
			_folderSourceRepository = folderSourceRepository;
			_softwareService = new SoftwareService(_softwareRepository, _regionRepository);
			_logDirectory = Path.Combine(Logger.Instance.LOG_DIRECTORY, "Chrome");
			_driverManager = new WebDriverManager(_logDirectory);
			_regionResolver = new RegionResolver(_regionRepository.RegionRef);
		}

		/// <summary>
		/// Starts a timer to check if the scraping is taking too long.
		/// </summary>
		/// <param name="startTime"></param>
		/// <param name="progressEntry"></param>
		/// <returns></returns>
		/// <exception cref="TimeoutException"></exception>
		private async Task Timer(DateTime startTime, LogEntry progressEntry)
		{
			var timerCT = _timerCts.Token;
			var lastReported = -1;
			while (!_cancellationToken.IsCancellationRequested)
			{
				timerCT.ThrowIfCancellationRequested();
				var elapsedTime = (int)(DateTime.UtcNow - startTime).TotalSeconds;
				if (elapsedTime != lastReported)
				{
					lastReported = elapsedTime;
					UpdateProgressEntry(progressEntry, elapsedTime, null);
				}

				await Task.Delay(1000, timerCT);

				if (elapsedTime > 89)
				{
					UpdateProgressEntry(progressEntry, elapsedTime, "Timed out");
					throw new TimeoutException("Scraping is taking too long.");
				}
			}
		}

		/// <summary>
		/// Scrapes the Xiaomi firmware page.
		/// <para>
		/// Scrapes for "fastboot-list" div that contains "a" tag elements.
		/// The structure of the string is:
		/// </para>
		/// <para> - The (name) is until a word "Latest"</para>
		/// <para> - From "Latest" until a word "Version" is the (region) </para>
		/// <para> - Href is the download link (web version) </para>
		/// </summary>
		/// <returns></returns>
		public async Task<IReadOnlyList<ScrapeIssue>> Scrape()
		{
			ScrapeStart?.Invoke();
			var startTime = DateTime.UtcNow;
			var progressEntry = new LogEntry("Scraping in progress", "Elapsed: 0s", level: AppLogLevel.Task);
			var progressLog = Logger.Instance.Log(progressEntry);
			var hasError = false;
			string? errorMessage = null;
			var wasCanceled = false;

			progressLog.AddDetail("Target URL", ScraperConfig.XiaomiFirmwareUrl, AppLogLevel.Debug);
			progressLog.AddDetail("Timeout", ScraperConfig.DefaultTimeout.ToString(), AppLogLevel.Debug);
			progressLog.AddDetail("Chrome logs", _logDirectory, AppLogLevel.Debug);
			_ = Timer(startTime, progressEntry);

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
			cts.CancelAfter(ScraperConfig.DefaultTimeout);

			try
			{
				progressLog.AddDetail("Starting browser session.");
				using var driver = _driverManager.GetDriver(progressLog);
				progressLog.AddDetail("Browser session ready.");
				_cancellationToken.ThrowIfCancellationRequested();

				progressLog.AddDetail("Navigating to Xiaomi firmware page.");
				var pageInteraction = new WebPageInteraction(driver);
				pageInteraction.NavigateToPage(ScraperConfig.XiaomiFirmwareUrl);
				progressLog.AddDetail("Page ready.");
				progressLog.AddDetail("Page source length", driver.PageSource.Length.ToString(), AppLogLevel.Debug);

				progressLog.AddDetail("Checking cookie banner.", level: AppLogLevel.Debug);
				if (pageInteraction.TryAcceptCookies())
				{
					progressLog.AddDetail("Cookies accepted.");
				}
				else
				{
					progressLog.AddDetail("Cookie banner not detected.", level: AppLogLevel.Debug);
				}

				try
				{
					progressLog.AddDetail("Selecting Fastboot Update tab.");
					pageInteraction.ClickFastbootUpdate();
					_cancellationToken.ThrowIfCancellationRequested();
					progressLog.AddDetail("Fastboot Update tab selected.");
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					hasError = true;
					errorMessage = "Failed to select Fastboot Update tab.";
					progressLog.AddDetail(errorMessage, ex.Message, AppLogLevel.Error);
					AddExceptionDetails(progressLog, ex);
					throw;
				}

				_cancellationToken.ThrowIfCancellationRequested();

				try
				{
					progressLog.AddDetail("Extracting fastboot entries.");
					var linkData = pageInteraction.ExtractFastbootLinks();
					progressLog.AddDetail("Raw entries", linkData.Count.ToString(), AppLogLevel.Debug);

					var duplicateGroups = linkData
						.GroupBy(link => ScrapeTextNormalizer.NormalizeLinkText(link.Text), StringComparer.OrdinalIgnoreCase)
						.Where(group => group.Count() > 1)
						.ToList();
					var duplicateEntries = duplicateGroups.Sum(group => group.Count() - 1);
					progressLog.AddDetail("Duplicate groups", duplicateGroups.Count.ToString(), AppLogLevel.Debug);
					progressLog.AddDetail("Duplicate entries", duplicateEntries.ToString(), AppLogLevel.Debug);

					var mergedLinks = MergeDuplicateLinks(linkData, progressLog);
					progressLog.AddDetail("Entries after merge", mergedLinks.Count.ToString());

					progressLog.AddDetail("Seeding regions.");
					var seedSummary = await SeedRegions(mergedLinks);
					progressLog.AddDetail("Region tokens", seedSummary.CandidateCount.ToString(), AppLogLevel.Debug);
					progressLog.AddDetail("Unique regions", seedSummary.UniqueCount.ToString(), AppLogLevel.Debug);
					progressLog.AddDetail("Added", seedSummary.AddedCount.ToString());
					progressLog.AddDetail("Already present", seedSummary.ExistingCount.ToString(), AppLogLevel.Debug);

					var regions = await _regionRepository.GetAllAsync();
					progressLog.AddDetail("Regions available", regions.Count.ToString(), AppLogLevel.Debug);

					progressLog.AddDetail("Processing software entries.");
					var processSummary = await ProcessSoftwareEntries(mergedLinks, regions);
					progressLog.AddDetail("Total entries", processSummary.Total.ToString());
					progressLog.AddDetail("Parsed OK", processSummary.Success.ToString());
					progressLog.AddDetail("Created", processSummary.Created.ToString(), AppLogLevel.Debug);
					progressLog.AddDetail("Updated", processSummary.Updated.ToString(), AppLogLevel.Debug);
					progressLog.AddDetail("Issues", processSummary.Issues.ToString(),
						processSummary.Issues > 0 ? AppLogLevel.Warning : AppLogLevel.Info);

					if (IrregularSoftware.Count > 0)
					{
						foreach (var issue in IrregularSoftware)
						{
							var details = new List<LogEntry>
							{
								new("Link Text", issue.LinkText, level: AppLogLevel.Debug),
								new("Download Link", issue.LinkHref, level: AppLogLevel.Debug),
								new("HTML", issue.Html, level: AppLogLevel.Debug)
							};

							progressLog.AddDetail(new LogEntry("Irregular entry", issue.Reason, details, AppLogLevel.Warning));
						}
					}
				}
				catch (WebDriverTimeoutException ex)
				{
					hasError = true;
					errorMessage = "Timeout waiting for required page elements.";
					progressLog.AddDetail(errorMessage, ex.Message, AppLogLevel.Error);
					AddExceptionDetails(progressLog, ex);
					pageInteraction.TakeErrorScreenshot(_logDirectory);
					throw;
				}
				catch (Exception ex)
				{
					hasError = true;
					errorMessage = "Error while extracting or processing elements.";
					progressLog.AddDetail(errorMessage, ex.Message, AppLogLevel.Error);
					AddExceptionDetails(progressLog, ex);
					throw;
				}
			}
			catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
			{
				wasCanceled = true;
				progressLog.AddDetail("Scraper canceled by user.", level: AppLogLevel.Warning);
				throw;
			}
			catch (Exception ex)
			{
				hasError = true;
				errorMessage ??= "Scraper failed.";
				progressLog.AddDetail(errorMessage, ex.Message, AppLogLevel.Error);
				AddExceptionDetails(progressLog, ex);
				throw;
			}
			finally
			{
				cts.Cancel();
				_timerCts.Cancel();

				var elapsed = DateTime.UtcNow - startTime;
				var elapsedSeconds = (int)elapsed.TotalSeconds;
				var status = wasCanceled ? "Canceled" : hasError ? "Failed" : "Finished";
				UpdateProgressEntry(progressEntry, elapsedSeconds, status);
				var summaryLabel = wasCanceled ? "Scraper canceled" : "Scraper finished";
				progressLog.AddDetail(summaryLabel, $"Elapsed: {elapsed.TotalSeconds:0}s",
					wasCanceled ? AppLogLevel.Warning : AppLogLevel.Info);
				progressLog.AddDetail("Issues", IrregularSoftware.Count.ToString(),
					IrregularSoftware.Count > 0 ? AppLogLevel.Warning : AppLogLevel.Info);

				if (wasCanceled)
				{
					Logger.Instance.Log("Scraper canceled.", AppLogLevel.Warning);
				}
				else if (hasError)
				{
					Logger.Instance.Log("Scraper failed.", AppLogLevel.Error, errorMessage);
				}
				else
				{
					Logger.Instance.Log("Scraper finished.");
				}

				ScrapeEnd?.Invoke();
			}

			return IrregularSoftware.AsReadOnly();
		}

		/// <summary>
		/// Processes the software entries and updates the database with the new data.
		/// If there are any software entries which name, region or version could not be extracted, they are added to the IrregularSoftware list.
		/// </summary>
		/// <param name="linkData"></param>
		/// <param name="regions"></param>
		/// <returns></returns>
		private async Task<ProcessSummary> ProcessSoftwareEntries(IReadOnlyList<ScrapeLink> linkData, List<Region> regions)
		{
			var total = 0;
			var success = 0;
			var created = 0;
			var updated = 0;
			var issues = 0;

			foreach (var link in linkData)
			{
				_cancellationToken.ThrowIfCancellationRequested();
				total++;

				ParseResult result;
				try
				{
					result = ProcessSoftwareEntry(link, regions);
				}
				catch (Exception ex)
				{
					IrregularSoftware.Add(new ScrapeIssue(
						link.Text,
						link.Href,
						link.Html,
						$"Parser exception: {ex.Message}"));
					issues++;
					continue;
				}

				if (result.Software == null)
				{
					if (!string.IsNullOrWhiteSpace(result.FailureReason))
					{
						IrregularSoftware.Add(new ScrapeIssue(
							link.Text,
							link.Href,
							link.Html,
							result.FailureReason));
					}
					issues++;
					continue;
				}

				var outcome = await UpdateSoftwareInDatabase(result.Software);
				success++;
				switch (outcome)
				{
					case UpdateOutcome.Created:
						created++;
						break;
					case UpdateOutcome.Updated:
						updated++;
						break;
				}
			}

			return new ProcessSummary(total, success, created, updated, issues);
		}

		/// <summary>
		/// Processes the software entry and returns the software object. If it fails to extract the name, region or version it returns null.
		/// </summary>
		/// <param name="link"></param>
		/// <param name="regions"></param>
		/// <returns></returns>
		private ParseResult ProcessSoftwareEntry(ScrapeLink link, IReadOnlyList<Region> regions)
		{
			if (string.IsNullOrWhiteSpace(link.Text))
			{
				return new ParseResult(null, "Entry text is empty.");
			}

			var normalizedText = ScrapeTextNormalizer.NormalizeLinkText(link.Text);

			// Extract Name
			var name = ExtractName(normalizedText);
			if (string.IsNullOrWhiteSpace(name))
			{
				return new ParseResult(null, "Failed to extract model name (missing 'Latest' token).");
			}

			// Extract Region
			if (!TryExtractRegionToken(normalizedText, out var regionToken))
			{
				return new ParseResult(null, "Failed to extract region (missing 'Latest'/'Version' tokens).");
			}

			if (!_regionResolver.TryResolveForSoftware(regionToken, regions, out var regionAcronym, out var reason) ||
				string.IsNullOrWhiteSpace(regionAcronym))
			{
				return new ParseResult(null, reason ?? "Failed to resolve region.");
			}

			// Extract Version
			if (string.IsNullOrWhiteSpace(link.Href))
			{
				return new ParseResult(null, "Download link is missing.");
			}

			if (!SoftwareVersion.TryParse(link.Href, out var version))
			{
				return new ParseResult(null, "Failed to extract version from download link.");
			}

			string? codename = null;
			if (SoftwareLinkParser.TryExtractCodename(link.Href, out var parsedCodename))
			{
				codename = parsedCodename;
			}

			return new ParseResult(new Software
			{
				Name = name,
				Codename = codename,
				Regions = regions.Where(x => x.Acronym == regionAcronym).ToList(),
				WebLink = link.Href,
				WebVersion = version.Raw
			}, null);
		}

		/// <summary>
		/// Extracts the name from the link name.
		/// </summary>
		/// <param name="linkName"></param>
		/// <returns></returns>
		private static string? ExtractName(string linkName)
		{
			var name = LogicHelpers.GetSubstring(linkName, "Latest");
			if (string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			// If the name contains the separation symbol ("/"), means it defines more than one model.
			// Make sure there are no whitespaces before and after it.
			return Regex.Replace(name, @"\s*/\s*", "/").Trim();
		}

		private static bool TryExtractRegionToken(string linkName, out string regionToken)
		{
			regionToken = string.Empty;
			var token = LogicHelpers.GetStringBetweenWords(linkName, "Latest", "Version");
			if (string.IsNullOrWhiteSpace(token))
			{
				return false;
			}

			regionToken = ScrapeTextNormalizer.NormalizeRegionToken(token);
			return !string.IsNullOrWhiteSpace(regionToken);
		}

		/// <summary>
		/// Updates existing software in the database or adds new software. Also updates the local version if one exists.
		/// </summary>
		/// <param name="software"></param>
		/// <returns></returns>
		private async Task<UpdateOutcome> UpdateSoftwareInDatabase(Software software)
		{
			var region = software.Regions.FirstOrDefault()?.Acronym;
			if (region == null)
			{
				return UpdateOutcome.Updated;
			}

			var existingSoftware = await _softwareRepository.GetByNameAndRegionAcronymAsync(software.Name, region);

			if (existingSoftware == null)
			{
				await _softwareService.AddSoftwareWithRegionAsync(software, region);
			}
			else
			{
				existingSoftware.WebLink = software.WebLink;
				existingSoftware.WebVersion = software.WebVersion;
				existingSoftware.Codename = software.Codename;
				await _softwareRepository.UpdateAsync(existingSoftware);
			}

			var dbHelper = new DatabaseHelpers(_folderSourceRepository, _softwareRepository);
			await dbHelper.UpdateLocalVersionAsync(software.Name, region);

			return existingSoftware == null ? UpdateOutcome.Created : UpdateOutcome.Updated;
		}

		/// <summary>
		/// Seeds unique regions to the database.
		/// </summary>
		/// <param name="linkData"></param>
		/// <returns></returns>
		private async Task<SeedSummary> SeedRegions(IEnumerable<ScrapeLink> linkData)
		{
			var regionList = new List<Region>();
			var seenAcronyms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var candidateCount = 0;

			foreach (var link in linkData)
			{
				var normalizedText = ScrapeTextNormalizer.NormalizeLinkText(link.Text);
				if (!TryExtractRegionToken(normalizedText, out var regionToken))
				{
					continue;
				}

				candidateCount++;

				if (!_regionResolver.TryResolveForSeeding(regionToken, out var region, out _))
				{
					continue;
				}

				if (region != null && seenAcronyms.Add(region.Acronym))
				{
					regionList.Add(region);
				}
			}

			var added = 0;
			var existing = 0;

			foreach (var region in regionList)
			{
				if (await _regionRepository.GetRegionByNameAsync(region.Name) == null)
				{
					await _regionRepository.AddAsync(region);
					added++;
				}
				else
				{
					existing++;
				}
			}

			return new SeedSummary(candidateCount, regionList.Count, added, existing);
		}

		private static List<ScrapeLink> MergeDuplicateLinks(
			IReadOnlyList<ScrapeLink> linkData,
			Logger.LogHandle logHandle)
		{
			var grouped = linkData
				.GroupBy(link => ScrapeTextNormalizer.NormalizeLinkText(link.Text), StringComparer.OrdinalIgnoreCase)
				.ToList();

			foreach (var group in grouped.Where(g => g.Count() > 1))
			{
				var originalText = group.First().Text;
				logHandle.AddDetail(
					"Duplicate entry merged",
					$"'{originalText}' appears {group.Count()} times. Using latest version link.",
					AppLogLevel.Warning);
			}

			var merged = new List<ScrapeLink>();
			foreach (var group in grouped)
			{
				if (group.Count() == 1)
				{
					merged.Add(group.First());
					continue;
				}

				var candidates = group
					.Select(link => (link, version: SoftwareVersion.TryParse(link.Href ?? string.Empty, out var version)
						? version
						: null))
					.ToList();

				var best = candidates
					.Where(candidate => candidate.version != null)
					.OrderByDescending(candidate => candidate.version)
					.Select(candidate => candidate.link)
					.FirstOrDefault();

				merged.Add(best ?? candidates.First().link);
			}

			return merged;
		}

		private static void AddExceptionDetails(Logger.LogHandle logHandle, Exception exception)
		{
			var current = exception;
			var depth = 0;

			while (current != null)
			{
				var prefix = depth == 0 ? "Exception" : $"Inner exception {depth}";
				logHandle.AddDetail(prefix, current.GetType().FullName ?? "Unknown", AppLogLevel.Debug);
				logHandle.AddDetail("Message", current.Message, AppLogLevel.Error);

				if (!string.IsNullOrWhiteSpace(current.StackTrace))
				{
					logHandle.AddDetail("Stack Trace", current.StackTrace, AppLogLevel.Debug);
				}

				current = current.InnerException;
				depth++;
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

		public void Dispose()
		{
			_timerCts.Dispose();
			_driverManager.Dispose();
		}

		private sealed record ParseResult(Software? Software, string? FailureReason);

		private sealed record SeedSummary(int CandidateCount, int UniqueCount, int AddedCount, int ExistingCount);

		private sealed record ProcessSummary(int Total, int Success, int Created, int Updated, int Issues);

		private enum UpdateOutcome
		{
			Created,
			Updated
		}
	}
}

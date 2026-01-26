using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Data;
using xsm.Data.Repositories;
using xsm.Logic;
using xsm.Logic.Downloads;
using xsm.Logic.Helpers;
using xsm.Logic.LocalSoftware;
using xsm.Logic.Mirrors;
using xsm.Logic.Scraper;
using xsm.Logic.Updates;
using xsm.Models;
using xsm.UI.Views.Dialogs;
using xsm.ViewModels;
using xsm.ViewModels.Windows;

namespace xsm.UI.Views.Windows;

public partial class MainWindow : Window
{
	private bool _hasInitialized;
	private bool _shutdownInProgress;
	private bool _allowClose;
	private CancellationTokenSource? _localFolderSizeCts;
	private DownloadManagerWindow? _downloadManagerWindow;
	private readonly UpdateManager _updateManager = new();
	private const string SoftwareGridStateKey = "software.grid.state";
	private static readonly JsonSerializerOptions GridStateSerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() }
	};
	private bool _suppressGridStateSave;
	private SoftwareGridState? _pendingGridState;

	public MainWindow()
	{
		InitializeComponent();
		var viewModel = new MainWindowViewModel();
		viewModel.GridStateChanged += ViewModel_OnGridStateChanged;
		DataContext = viewModel;
		Opened += MainWindow_OnOpened;
		Closing += MainWindow_OnClosing;
	}

	private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;
	private CancellationToken ShutdownToken => AppLifecycle.Instance.ShutdownToken;

	private void MainWindow_OnOpened(object? sender, EventArgs e)
	{
		if (_hasInitialized)
		{
			return;
		}

		_hasInitialized = true;
		ViewModel.IsInitializing = true;
		ViewModel.LoadingMessage = "Starting up...";
		Dispatcher.UIThread.Post(() => _ = InitializeAsync(), DispatcherPriority.Background);
	}

	private async Task InitializeAsync()
	{
		ViewModel.IsInitializing = true;
		var cancellationToken = ShutdownToken;

		try
		{
			await Task.Yield();
			ViewModel.LoadingMessage = "Loading settings...";
			_pendingGridState = await LoadSettingsAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			ViewModel.LoadingMessage = "Checking downloads...";
			await DownloadManagerService.Instance.ResetStaleDownloadingFlagsAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			ViewModel.LoadingMessage = "Loading software...";
			await LoadSoftwareAsync(cancellationToken);
			ApplyPendingGridState();
			cancellationToken.ThrowIfCancellationRequested();

			ViewModel.LoadingMessage = "Reading local storage stats...";
			await UpdateLocalStatsAsync();
			cancellationToken.ThrowIfCancellationRequested();

			ViewModel.LoadingMessage = "Loading download domains...";
			await LoadDownloadDomainsAsync(true, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			ViewModel.LoadingMessage = "Rating mirrors...";
			await RunMirrorRatingAsync(false, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			Logger.Instance.Log("Initialization canceled.", LogLevel.Warning);
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, "Initialization failed.", LogLevel.Error);
		}
		finally
		{
			if (!AppLifecycle.Instance.IsShutdownRequested)
			{
				ViewModel.IsInitializing = false;
				ViewModel.LoadingMessage = string.Empty;
			}
		}
	}

	private async void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
	{
		if (_allowClose)
		{
				return;
			}

			e.Cancel = true;
			if (_shutdownInProgress)
			{
				return;
			}

			if (DownloadManagerService.Instance.ViewModel.HasActiveDownloads)
			{
				var confirm = new ConfirmCloseDialog();
				var shouldClose = await confirm.ShowDialog<bool>(this);
				if (!shouldClose)
				{
					return;
				}
			}

			_shutdownInProgress = true;
			ViewModel.IsInitializing = true;
			ViewModel.LoadingMessage = "Shutting down...";

		try
		{
			await AppLifecycle.Instance.ShutdownAsync(TimeSpan.FromSeconds(5));
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, "Shutdown failed.", LogLevel.Error);
		}
		finally
		{
			if (ViewModel is IDisposable disposable)
			{
				disposable.Dispose();
			}

			if (_downloadManagerWindow != null)
			{
				_downloadManagerWindow.AllowClose();
				_downloadManagerWindow.Close();
			}

			_allowClose = true;
			Close();
		}
	}

	private async void ViewModel_OnGridStateChanged(object? sender, EventArgs e)
	{
		if (_suppressGridStateSave)
		{
			return;
		}

		try
		{
			await SaveGridStateAsync(ShutdownToken);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, "Failed to save grid state.", LogLevel.Warning);
		}
	}

	private void ApplyPendingGridState()
	{
		if (_pendingGridState == null)
		{
			return;
		}

		_suppressGridStateSave = true;
		try
		{
		ViewModel.ApplyGridState(_pendingGridState);
		}
		finally
		{
			_suppressGridStateSave = false;
			_pendingGridState = null;
		}
	}

	private async void CheckForUpdates_OnClick(object? sender, RoutedEventArgs e)
	{
		if (ViewModel.IsCheckingForUpdates)
		{
			return;
		}

		if (DownloadManagerService.Instance.ViewModel.HasActiveDownloads)
		{
			ViewModel.UpdateStatusMessage = "Stop active downloads before updating.";
			return;
		}

		ViewModel.IsCheckingForUpdates = true;
		ViewModel.UpdateStatusMessage = "Checking for updates...";

		try
		{
			var result = await _updateManager.CheckForUpdatesAsync(
				ViewModel.AppVersion,
				ViewModel.IncludePrereleaseUpdates);

			ViewModel.UpdateStatusMessage = result.StatusMessage;
			if (!result.IsUpdateAvailable || result.Asset == null || result.LatestVersion == null)
			{
				return;
			}

			ViewModel.UpdateStatusMessage = $"Downloading {result.LatestVersion}...";
			var zipPath = await _updateManager.DownloadAssetAsync(result.Asset, AppContext.BaseDirectory);
			if (string.IsNullOrWhiteSpace(zipPath))
			{
				ViewModel.UpdateStatusMessage = "Failed to download the update package.";
				return;
			}

			var executablePath = Environment.ProcessPath;
			if (string.IsNullOrWhiteSpace(executablePath))
			{
				ViewModel.UpdateStatusMessage = "Executable path is unavailable.";
				return;
			}

			var updaterPath = Path.Combine(AppContext.BaseDirectory, UpdateManager.UpdaterExeName);
			if (!File.Exists(updaterPath))
			{
				ViewModel.UpdateStatusMessage = "Updater is missing in the application folder.";
				return;
			}

			if (!_updateManager.TryLaunchUpdater(
				updaterPath,
				Environment.ProcessId,
				executablePath,
				zipPath,
				out var error))
			{
				ViewModel.UpdateStatusMessage = $"Failed to start updater: {error}";
				return;
			}

			await ShutdownForUpdateAsync();
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, "Update check failed.", LogLevel.Error);
			ViewModel.UpdateStatusMessage = "Update check failed.";
		}
		finally
		{
			ViewModel.IsCheckingForUpdates = false;
		}
	}

	private async Task ShutdownForUpdateAsync()
	{
		try
		{
			await AppLifecycle.Instance.ShutdownAsync(TimeSpan.FromSeconds(5));
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, "Shutdown failed before update.", LogLevel.Error);
		}
		finally
		{
			if (ViewModel is IDisposable disposable)
			{
				disposable.Dispose();
			}

			if (_downloadManagerWindow != null)
			{
				_downloadManagerWindow.AllowClose();
				_downloadManagerWindow.Close();
			}

			_allowClose = true;
			Close();
		}
	}

	private async void FetchData_OnClick(object? sender, RoutedEventArgs e)
	{
		if (!ViewModel.CanScrape)
		{
			Logger.Instance.Log("Select a local software folder before scraping.", LogLevel.Warning);
			return;
		}

		Logger.Instance.Log("Fetch Data clicked.");
		ViewModel.IsScraping = true;

		try
		{
			var issues = await Runner.Instance.StartScraperAsync(ShutdownToken);
			if (issues.Count > 0)
			{
				var dialog = new ScrapeIssuesDialog(issues);
				await dialog.ShowDialog(this);
			}

			await LoadSoftwareAsync(ShutdownToken);
		}
		catch (OperationCanceledException)
		{
			Logger.Instance.Log("Scraper canceled.", LogLevel.Warning);
		}
		finally
		{
			ViewModel.IsScraping = false;
		}
	}

	private void Search_OnClick(object? sender, RoutedEventArgs e)
	{
		ApplySearchWithLogging();
	}

	private void SearchBox_OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
		{
			return;
		}

		ApplySearchWithLogging();
		e.Handled = true;
	}

	private void ApplySearchWithLogging()
	{
		var term = ViewModel.SearchText?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(term))
		{
			Logger.Instance.Log("Search cleared.");
		}
		else
		{
			Logger.Instance.Log($"Search applied: \"{term}\".");
		}

		ViewModel.ApplySearch();
	}

	private async void TestDomains_OnClick(object? sender, RoutedEventArgs e)
	{
		await RunMirrorRatingAsync(true, ShutdownToken);
	}

	private async void RefreshLocal_OnClick(object? sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(ViewModel.LocalSoftwarePath))
		{
			Logger.Instance.Log("Select a local software folder before refreshing.", LogLevel.Warning);
			return;
		}

		if (ViewModel.IsRefreshingLocal)
		{
			return;
		}

		Logger.Instance.Log("Refreshing local software list.");
		ViewModel.IsRefreshingLocal = true;
		try
		{
			var syncService = new LocalSoftwareSyncService();
			var summary = await syncService.RefreshAllAsync(ViewModel.LocalSoftwarePath, ShutdownToken);
			Logger.Instance.Log($"Local software scan completed. Updated {summary.Updated}/{summary.Total} entries.",
				LogLevel.Info);
			await LoadSoftwareAsync(ShutdownToken);
			await UpdateLocalStatsAsync();
		}
		catch (OperationCanceledException)
		{
			Logger.Instance.Log("Local software scan canceled.", LogLevel.Warning);
		}
		finally
		{
			ViewModel.IsRefreshingLocal = false;
		}
	}

	private void StopScrape_OnClick(object? sender, RoutedEventArgs e)
	{
		if (!ViewModel.IsScraping)
		{
			return;
		}

		if (Runner.Instance.RequestStop())
		{
			Logger.Instance.Log("Stop requested. Canceling scrape...", LogLevel.Warning);
		}
	}

	private async void SelectLocalFolder_OnClick(object? sender, RoutedEventArgs e)
	{
		if (StorageProvider == null)
		{
			return;
		}

		var selection = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			AllowMultiple = false,
			Title = "Select local software folder"
		});

		var folder = selection.FirstOrDefault();
		if (folder == null)
		{
			return;
		}

		var path = folder.Path.LocalPath;
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		try
		{
			await SaveLocalFolderPathAsync(path, ShutdownToken);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		ViewModel.LocalSoftwarePath = path;
		DownloadManagerService.Instance.LocalSoftwarePath = path;
		await UpdateLocalStatsAsync();
		_ = UpdateLocalFolderSizesAsync(ShutdownToken);
	}

	private static async Task SaveLocalFolderPathAsync(string path, CancellationToken cancellationToken = default)
	{
		await using var context = AppDbContextFactory.Create();
		await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

		var repository = new FolderSourceRepository(context);
		var existing = await repository.GetByNameAsync(FolderSourceDefaults.LocalSoftwareName);
		if (existing == null)
		{
			await repository.AddAsync(new FolderSource
			{
				Name = FolderSourceDefaults.LocalSoftwareName,
				Path = path
			});
			return;
		}

		if (string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		existing.Path = path;
		await repository.UpdateAsync(existing);
	}

	private async Task<SoftwareGridState?> LoadSettingsAsync(CancellationToken cancellationToken = default)
	{
		ViewModel.LoadingMessage = "Preparing database...";
		await Task.Yield();

		await using var context = AppDbContextFactory.Create();
		await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

		ViewModel.LoadingMessage = "Loading local software folder...";
		await Task.Yield();
		var folderRepository = new FolderSourceRepository(context);
		var existing = await folderRepository.GetByNameAsync(FolderSourceDefaults.LocalSoftwareName);
		if (existing != null)
		{
			ViewModel.LocalSoftwarePath = existing.Path;
			DownloadManagerService.Instance.LocalSoftwarePath = existing.Path;
		}

		ViewModel.LoadingMessage = "Loading grid filters...";
		await Task.Yield();
		var repository = new AppSettingRepository(context);
		var setting = await repository.GetByKeyAsync(SoftwareGridStateKey);
		if (string.IsNullOrWhiteSpace(setting?.Value))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<SoftwareGridState>(setting.Value, GridStateSerializerOptions);
		}
		catch (JsonException ex)
		{
			Logger.Instance.LogException(ex, "Failed to parse saved grid state.", LogLevel.Warning);
			return null;
		}
	}

	private async Task SaveGridStateAsync(CancellationToken cancellationToken = default)
	{
		var state = ViewModel.GetGridState();
		var payload = JsonSerializer.Serialize(state, GridStateSerializerOptions);

		await using var context = AppDbContextFactory.Create();
		await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

		var repository = new AppSettingRepository(context);
		await repository.SetAsync(SoftwareGridStateKey, payload);
	}

	private async Task LoadSoftwareAsync(CancellationToken cancellationToken = default)
	{
		await using var context = AppDbContextFactory.Create();
		await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

		var repository = new SoftwareRepository(context);
		var software = await repository.GetAllAsync();
		ViewModel.SetSoftwareItems(software);
		Dispatcher.UIThread.Post(AutoSizeSoftwareColumns, DispatcherPriority.Loaded);
		_ = UpdateLocalFolderSizesAsync(cancellationToken);
	}

	private async Task UpdateLocalStatsAsync()
	{
		var localPath = ViewModel.LocalSoftwarePath;
		if (string.IsNullOrWhiteSpace(localPath))
		{
			ViewModel.UpdateLocalStats("--", "--");
			DownloadManagerService.Instance.UpdateDiskStats(new LocalSoftwareStats(null, null, null));
			return;
		}

		var stats = await Task.Run(() => LocalSoftwareStatsProvider.GetStats(localPath), ShutdownToken);
		var diskText = stats.DriveTotalBytes.HasValue && stats.DriveFreeBytes.HasValue
			? $"Disk: {ByteSizeFormatter.FormatBytes(stats.DriveFreeBytes.Value)} free / {ByteSizeFormatter.FormatBytes(stats.DriveTotalBytes.Value)}"
			: "--";
		var localText = stats.FolderSizeBytes.HasValue
			? $"Local: {ByteSizeFormatter.FormatBytes(stats.FolderSizeBytes.Value)}"
			: "--";

		ViewModel.UpdateLocalStats(diskText, localText);
		DownloadManagerService.Instance.UpdateDiskStats(stats);
	}

	private async Task UpdateLocalFolderSizesAsync(CancellationToken cancellationToken = default)
	{
		_localFolderSizeCts?.Cancel();
		_localFolderSizeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var linkedToken = _localFolderSizeCts.Token;

		var localPath = ViewModel.LocalSoftwarePath;
		var items = ViewModel.GetAllSoftwareItemsSnapshot();
		if (items.Count == 0)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(localPath))
		{
			Dispatcher.UIThread.Post(() =>
			{
				foreach (var item in items)
				{
					item.UpdateLocalFolderSize(null);
				}
			}, DispatcherPriority.Background);
			return;
		}

		List<(SoftwareRowViewModel Item, long? Size)> results;
		try
		{
			results = await Task.Run(() =>
			{
				var sizes = new List<(SoftwareRowViewModel Item, long? Size)>(items.Count);
				foreach (var item in items)
				{
					linkedToken.ThrowIfCancellationRequested();
					var size = LocalSoftwareScanner.TryGetModelFolderSize(localPath, item.Name, item.RegionAcronym);
					sizes.Add((item, size));
				}

				return sizes;
			}, linkedToken);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		if (linkedToken.IsCancellationRequested)
		{
			return;
		}

		Dispatcher.UIThread.Post(() =>
		{
			foreach (var entry in results)
			{
				entry.Item.UpdateLocalFolderSize(entry.Size);
			}

			AutoSizeSoftwareColumns();
		}, DispatcherPriority.Background);
	}

	private async Task LoadDownloadDomainsAsync(bool seed, CancellationToken cancellationToken = default)
	{
		await using var context = AppDbContextFactory.Create();
		await DatabaseBootstrapper.EnsureSchemaAsync(context, cancellationToken);

		if (seed)
		{
			await DownloadDomainSeedService.SeedAsync(context, null, cancellationToken);
		}

		var repository = new DownloadDomainRepository(context);
		var domains = await repository.GetAllAsync();
		ViewModel.SetDownloadDomains(domains.Select(DownloadDomainItem.FromDomain));
	}

	private async Task RunMirrorRatingAsync(bool force, CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}

		ViewModel.IsRatingDomains = true;
		try
		{
			await MirrorRatingRunner.Instance.StartRatingAsync(force, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			Logger.Instance.Log("Mirror rating canceled.", LogLevel.Warning);
		}
		finally
		{
			ViewModel.IsRatingDomains = false;
		}

		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}

		try
		{
			await LoadDownloadDomainsAsync(false, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			Logger.Instance.Log("Mirror list reload canceled.", LogLevel.Warning);
		}
	}

	private async void DownloadSelected_OnClick(object? sender, RoutedEventArgs e)
	{
		if (!ViewModel.CanDownloadSelected)
		{
			return;
		}

		var selected = ViewModel.GetSelectedSoftware();
		if (selected.Count == 0)
		{
			Logger.Instance.Log("Select at least one software item to download.", LogLevel.Warning);
			return;
		}

		DownloadManagerService.Instance.LocalSoftwarePath = ViewModel.LocalSoftwarePath;
		await DownloadManagerService.Instance.EnqueueDownloadsAsync(selected, ShutdownToken);
		ShowDownloadManagerWindow();
	}

	private void StopAllDownloads_OnClick(object? sender, RoutedEventArgs e)
	{
		DownloadManagerService.Instance.CancelAll();
	}

	private void OpenDownloadManager_OnClick(object? sender, RoutedEventArgs e)
	{
		ShowDownloadManagerWindow();
	}

	private void SelectOutdated_OnClick(object? sender, RoutedEventArgs e)
	{
		var selectedCount = 0;
		foreach (var item in ViewModel.SoftwareItems)
		{
			var shouldSelect = item.Comparison == VersionComparisonResult.WebNewer &&
				!string.IsNullOrWhiteSpace(item.LocalVersion);
			item.IsSelected = shouldSelect;
			if (shouldSelect)
			{
				selectedCount++;
			}
		}

		Logger.Instance.Log($"Selected {selectedCount} outdated entries.");
		ViewModel.SortByOutdatedFirst();
	}

	private void DeselectAll_OnClick(object? sender, RoutedEventArgs e)
	{
		ViewModel.ClearSelections();
		Logger.Instance.Log("Deselected all software entries.");
	}

	private void ResetFilters_OnClick(object? sender, RoutedEventArgs e)
	{
		Logger.Instance.Log("Resetting grid filters and sorting.");
		ViewModel.ResetGridState();
	}

	private void SoftwareGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
	{
		if (e.Column == null || string.IsNullOrWhiteSpace(e.Column.SortMemberPath))
		{
			return;
		}

		var isSameColumn = string.Equals(ViewModel.SortMemberPath, e.Column.SortMemberPath, StringComparison.Ordinal);
		var nextDirection = isSameColumn && ViewModel.SortDirection == ListSortDirection.Ascending
			? ListSortDirection.Descending
			: ListSortDirection.Ascending;

		ViewModel.SetSortStateFromGrid(e.Column.SortMemberPath, nextDirection);
	}

	private void SoftwareGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
	{
		if (e.Source is not Control source)
		{
			return;
		}

		var row = source.GetVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
		if (row?.DataContext is not SoftwareRowViewModel item)
		{
			return;
		}

		OpenModelFolder(item);
	}

	private void OpenModelFolder(SoftwareRowViewModel item)
	{
		var displayName = item.DisplayName;

		if (string.IsNullOrWhiteSpace(ViewModel.LocalSoftwarePath))
		{
			Logger.Instance.Log($"Cannot open folder for {displayName}. Local software folder is not set.", LogLevel.Error);
			return;
		}

		if (!LocalSoftwareScanner.TryGetModelFolderPath(ViewModel.LocalSoftwarePath, item.Name, item.RegionAcronym, out var folderPath))
		{
			Logger.Instance.Log($"Cannot open folder for {displayName}. Folder not found.", LogLevel.Error);
			return;
		}

		var latestPath = LocalSoftwareScanner.GetPreferredExtractedFolderPath(folderPath, item.LocalVersion);
		var targetPath = latestPath ?? folderPath;
		if (latestPath != null)
		{
			Logger.Instance.Log($"Opening folder for latest software of {displayName}.");
		}
		else
		{
			Logger.Instance.Log($"Opening folder for {displayName}.");
		}

		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = targetPath,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Logger.Instance.LogException(ex, $"Failed to open folder for {displayName}.", LogLevel.Error);
		}
	}

	private void ShowDownloadManagerWindow()
	{
		if (_downloadManagerWindow == null)
		{
			_downloadManagerWindow = new DownloadManagerWindow();
		}

		_downloadManagerWindow.ShowOwnedBy(this);
	}

	private void AutoSizeSoftwareColumns()
	{
		if (SoftwareGrid is null || ViewModel.SoftwareItems.Count == 0)
		{
			return;
		}

		const double cellPadding = 24;
		const double headerPadding = 36;

		var fontFamily = TextElement.GetFontFamily(SoftwareGrid);
		var fontSize = TextElement.GetFontSize(SoftwareGrid);
		var fontStyle = TextElement.GetFontStyle(SoftwareGrid);
		var fontWeight = TextElement.GetFontWeight(SoftwareGrid);
		var fontStretch = TextElement.GetFontStretch(SoftwareGrid);
		var headerFontWeight = FontWeight.SemiBold;

		foreach (var column in SoftwareGrid.Columns)
		{
			if (column is DataGridTemplateColumn)
			{
				continue;
			}

			if (column is not DataGridBoundColumn boundColumn)
			{
				continue;
			}

			if (boundColumn.Binding is null)
			{
				continue;
			}

			var headerText = column.Header?.ToString() ?? string.Empty;
			var maxWidth = MeasureTextWidth(headerText, fontFamily, fontSize, fontStyle, headerFontWeight, fontStretch) + headerPadding;

			if (boundColumn is DataGridCheckBoxColumn)
			{
				maxWidth = Math.Max(maxWidth, 32 + cellPadding);
			}
			else if (boundColumn.Binding is Binding binding && !string.IsNullOrWhiteSpace(binding.Path))
			{
				foreach (var item in ViewModel.SoftwareItems)
				{
					var value = GetBindingValue(item, binding.Path);
					var text = value?.ToString() ?? string.Empty;
					var width = MeasureTextWidth(text, fontFamily, fontSize, fontStyle, fontWeight, fontStretch) + cellPadding;
					if (width > maxWidth)
					{
						maxWidth = width;
					}
				}
			}

			if (column.MinWidth > maxWidth)
			{
				maxWidth = column.MinWidth;
			}

			column.Width = new DataGridLength(Math.Ceiling(maxWidth));
		}
	}

	private static object? GetBindingValue(object item, string path)
	{
		var current = item;
		foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
		{
			if (current is null)
			{
				return null;
			}

			var property = current.GetType().GetProperty(segment);
			if (property is null)
			{
				return null;
			}

			current = property.GetValue(current);
		}

		return current;
	}

	private static double MeasureTextWidth(string text, FontFamily fontFamily, double fontSize, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}

		var textBlock = new TextBlock
		{
			Text = text,
			FontFamily = fontFamily,
			FontSize = fontSize,
			FontStyle = fontStyle,
			FontWeight = fontWeight,
			FontStretch = fontStretch
		};

		textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		return textBlock.DesiredSize.Width;
	}
}

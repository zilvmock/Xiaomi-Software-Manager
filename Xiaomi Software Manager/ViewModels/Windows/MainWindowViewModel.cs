using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Threading;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Logic.Downloads;
using xsm.Logic.Helpers;
using xsm.Models;
using xsm.ViewModels;

namespace xsm.ViewModels.Windows;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
	private string _localSoftwarePath = string.Empty;
	private bool _isScraping;
	private bool _isRatingDomains;
	private bool _isRefreshingLocal;
	private int _selectedSoftwareCount;
	private bool _isInitializing = true;
	private string _loadingMessage = "Loading...";
	private bool _includePrereleaseUpdates;
	private bool _isCheckingForUpdates;
	private string _updateStatusMessage = string.Empty;
	private readonly List<SoftwareRowViewModel> _allSoftwareItems = new();
	private string _searchText = string.Empty;
	private string _appliedSearchText = string.Empty;
	private string _filterSummaryText = "Filters: none";
	private string? _sortMemberPath;
	private ListSortDirection? _sortDirection;
	private bool _isUpdatingFilters;
	private bool _suppressFilterEvents;
	private bool _suppressGridStateNotifications;
	private readonly NotifyCollectionChangedEventHandler _softwareItemsChangedHandler;
	private readonly PropertyChangedEventHandler _softwareItemPropertyChangedHandler;

	public ObservableCollection<LogEntry> Entries { get; } = new();

	public ObservableCollection<SoftwareRowViewModel> SoftwareItems { get; } = new();
	public ObservableCollection<DownloadDomainItem> DownloadDomains { get; } = new();
	public ObservableCollection<FilterOptionViewModel> RegionFilters { get; } = new();

	public int SoftwareCount => SoftwareItems.Count;

	public bool HasVisibleSoftware => SoftwareItems.Count > 0;

	public bool ShowNoResults => !HasVisibleSoftware;

	public int SelectedSoftwareCount
	{
		get => _selectedSoftwareCount;
		private set
		{
			if (!SetProperty(ref _selectedSoftwareCount, value))
			{
				return;
			}

			OnPropertyChanged(nameof(HasSelectedSoftware));
			OnPropertyChanged(nameof(CanDownloadSelected));
		}
	}

	public bool HasSelectedSoftware => SelectedSoftwareCount > 0;

	public string LocalSoftwarePath
	{
		get => _localSoftwarePath;
		set
		{
			if (SetProperty(ref _localSoftwarePath, value))
			{
				OnPropertyChanged(nameof(CanScrape));
				OnPropertyChanged(nameof(CanRefreshLocal));
				OnPropertyChanged(nameof(CanDownloadSelected));
			}
		}
	}

	public bool IsScraping
	{
		get => _isScraping;
		set
		{
			if (SetProperty(ref _isScraping, value))
			{
				OnPropertyChanged(nameof(CanScrape));
			}
		}
	}

	public bool IsRatingDomains
	{
		get => _isRatingDomains;
		set
		{
			if (SetProperty(ref _isRatingDomains, value))
			{
				OnPropertyChanged(nameof(CanScrape));
				OnPropertyChanged(nameof(CanDownloadSelected));
				OnPropertyChanged(nameof(CanTestDomains));
			}
		}
	}

	public bool IsRefreshingLocal
	{
		get => _isRefreshingLocal;
		set
		{
			if (SetProperty(ref _isRefreshingLocal, value))
			{
				OnPropertyChanged(nameof(CanRefreshLocal));
			}
		}
	}

	public bool IncludePrereleaseUpdates
	{
		get => _includePrereleaseUpdates;
		set => SetProperty(ref _includePrereleaseUpdates, value);
	}

	public bool IsCheckingForUpdates
	{
		get => _isCheckingForUpdates;
		set
		{
			if (SetProperty(ref _isCheckingForUpdates, value))
			{
				OnPropertyChanged(nameof(CanCheckForUpdates));
			}
		}
	}

	public bool CanCheckForUpdates => !IsCheckingForUpdates;

	public string UpdateStatusMessage
	{
		get => _updateStatusMessage;
		set => SetProperty(ref _updateStatusMessage, value);
	}

	public bool IsInitializing
	{
		get => _isInitializing;
		set
		{
			if (SetProperty(ref _isInitializing, value))
			{
				OnPropertyChanged(nameof(IsReady));
				OnPropertyChanged(nameof(CanScrape));
				OnPropertyChanged(nameof(CanDownloadSelected));
				OnPropertyChanged(nameof(CanTestDomains));
				OnPropertyChanged(nameof(CanRefreshLocal));
			}
		}
	}

	public bool IsReady => !IsInitializing;

	public string LoadingMessage
	{
		get => _loadingMessage;
		set => SetProperty(ref _loadingMessage, value);
	}

	public bool CanScrape => !IsInitializing && !IsScraping && !IsRatingDomains && !string.IsNullOrWhiteSpace(LocalSoftwarePath);

	public bool CanDownloadSelected => !IsInitializing && HasSelectedSoftware && !IsRatingDomains && !HasActiveDownloads &&
		!string.IsNullOrWhiteSpace(LocalSoftwarePath);

	public bool CanTestDomains => !IsInitializing && !IsRatingDomains;

	public bool CanRefreshLocal => !IsInitializing && !IsRefreshingLocal && !string.IsNullOrWhiteSpace(LocalSoftwarePath);

	public DownloadManagerViewModel DownloadManager { get; }

	public bool HasActiveDownloads => DownloadManager.HasActiveDownloads;

	public bool CanStopDownloads => DownloadManager.HasActiveDownloads;

	public string DiskStatsText { get; set; } = "--";

	public string LocalFolderSizeText { get; set; } = "--";

	public string AppVersion { get; } = ResolveAppVersion();

	public string AppVersionDisplay => GetVersionDisplay(AppVersion);

	public string AppBuildMetadata => GetBuildMetadata(AppVersion);

	public bool HasAppBuildMetadata => !string.IsNullOrWhiteSpace(AppBuildMetadata);

	public string SearchText
	{
		get => _searchText;
		set => SetProperty(ref _searchText, value);
	}

	public string FilterSummaryText
	{
		get => _filterSummaryText;
		private set => SetProperty(ref _filterSummaryText, value);
	}

	public string? SortMemberPath => _sortMemberPath;

	public ListSortDirection? SortDirection => _sortDirection;

	public MainWindowViewModel()
	{
		DownloadManager = DownloadManagerService.Instance.ViewModel;
		DownloadManager.PropertyChanged += OnDownloadManagerPropertyChanged;

		foreach (var entry in Logger.Instance.GetEntriesSnapshot())
		{
			Entries.Add(entry);
		}

		Logger.Instance.EntryLogged += OnEntryLogged;
		Logger.Instance.DetailLogged += OnDetailLogged;
		_softwareItemsChangedHandler = (_, _) =>
		{
			OnPropertyChanged(nameof(SoftwareCount));
			OnPropertyChanged(nameof(HasVisibleSoftware));
			OnPropertyChanged(nameof(ShowNoResults));
		};
		_softwareItemPropertyChangedHandler = OnSoftwareItemPropertyChanged;
		SoftwareItems.CollectionChanged += _softwareItemsChangedHandler;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	public event EventHandler? GridStateChanged;

	public void Dispose()
	{
		Logger.Instance.EntryLogged -= OnEntryLogged;
		Logger.Instance.DetailLogged -= OnDetailLogged;
		DownloadManager.PropertyChanged -= OnDownloadManagerPropertyChanged;
		foreach (var item in _allSoftwareItems)
		{
			item.PropertyChanged -= _softwareItemPropertyChangedHandler;
		}
		foreach (var option in RegionFilters)
		{
			option.PropertyChanged -= OnFilterOptionChanged;
		}
		SoftwareItems.CollectionChanged -= _softwareItemsChangedHandler;
	}

	private void OnEntryLogged(LogEntry entry)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			Entries.Add(entry);
			return;
		}

		Dispatcher.UIThread.Post(() => Entries.Add(entry));
	}

	private void OnDetailLogged(LogEntry parent, LogEntry detail)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			parent.Details.Add(detail);
			return;
		}

		Dispatcher.UIThread.Post(() => parent.Details.Add(detail));
	}

	private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (Equals(field, value))
		{
			return false;
		}

		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void SetSoftwareItems(IEnumerable<Software> softwareItems)
	{
		foreach (var item in _allSoftwareItems)
		{
			item.PropertyChanged -= _softwareItemPropertyChangedHandler;
		}

		_allSoftwareItems.Clear();
		SoftwareItems.Clear();
		foreach (var software in softwareItems)
		{
			var row = new SoftwareRowViewModel(software);
			row.PropertyChanged += _softwareItemPropertyChangedHandler;
			_allSoftwareItems.Add(row);
		}

		BuildFilterOptions();
		ApplyFiltersAndSearch();
	}

	public void SetDownloadDomains(IEnumerable<DownloadDomainItem> domains)
	{
		DownloadDomains.Clear();
		foreach (var domain in domains)
		{
			DownloadDomains.Add(domain);
		}
	}

	public IReadOnlyList<SoftwareRowViewModel> GetSelectedSoftware()
	{
		return SoftwareItems.Where(item => item.IsSelected).ToList();
	}

	public void ClearSelections()
	{
		foreach (var item in _allSoftwareItems)
		{
			item.IsSelected = false;
		}
	}

	public void UpdateLocalStats(string diskStatsText, string localFolderSizeText)
	{
		DiskStatsText = diskStatsText;
		LocalFolderSizeText = localFolderSizeText;
		OnPropertyChanged(nameof(DiskStatsText));
		OnPropertyChanged(nameof(LocalFolderSizeText));
	}

	public IReadOnlyList<SoftwareRowViewModel> GetAllSoftwareItemsSnapshot()
	{
		return _allSoftwareItems.ToList();
	}

	public void ApplySearch()
	{
		SetAppliedSearchText(SearchText);
		ApplyFiltersAndSearch();
		RaiseGridStateChanged();
	}

	public void SortByOutdatedFirst()
	{
		SetSortState("UpToDateText", ListSortDirection.Ascending, force: true);
	}

	public void SetSortState(string? sortMemberPath, ListSortDirection? sortDirection, bool force = false)
	{
		if (!force &&
			string.Equals(_sortMemberPath, sortMemberPath, StringComparison.Ordinal) &&
			_sortDirection == sortDirection)
		{
			return;
		}

		_sortMemberPath = sortMemberPath;
		_sortDirection = sortDirection;
		OnPropertyChanged(nameof(SortMemberPath));
		OnPropertyChanged(nameof(SortDirection));
		ApplyFiltersAndSearch();
		RaiseGridStateChanged();
	}

	public void SetSortStateFromGrid(string? sortMemberPath, ListSortDirection? sortDirection)
	{
		if (string.Equals(_sortMemberPath, sortMemberPath, StringComparison.Ordinal) &&
			_sortDirection == sortDirection)
		{
			return;
		}

		_sortMemberPath = sortMemberPath;
		_sortDirection = sortDirection;
		OnPropertyChanged(nameof(SortMemberPath));
		OnPropertyChanged(nameof(SortDirection));
		RaiseGridStateChanged();
	}

	public SoftwareGridState GetGridState()
	{
		var allOption = RegionFilters.FirstOrDefault(option => option.IsAllOption);
		var allSelected = allOption?.IsSelected ?? true;
		var selectedRegions = GetSelectedFilterValues(RegionFilters)
			.OrderBy(region => region, StringComparer.OrdinalIgnoreCase)
			.ToList();

		return new SoftwareGridState
		{
			SearchText = _appliedSearchText,
			RegionAllSelected = allSelected,
			SelectedRegions = selectedRegions,
			SortMemberPath = _sortMemberPath,
			SortDirection = _sortDirection
		};
	}

	public void ApplyGridState(SoftwareGridState? state)
	{
		if (state == null)
		{
			return;
		}

		_suppressFilterEvents = true;
		_suppressGridStateNotifications = true;
		try
		{
			var search = state.SearchText?.Trim() ?? string.Empty;
			SearchText = search;
			SetAppliedSearchText(search);
			ApplyRegionFilterState(state);

			_sortMemberPath = state.SortMemberPath;
			_sortDirection = state.SortDirection;
			OnPropertyChanged(nameof(SortMemberPath));
			OnPropertyChanged(nameof(SortDirection));

			ApplyFiltersAndSearch();
		}
		finally
		{
			_suppressFilterEvents = false;
			_suppressGridStateNotifications = false;
		}
	}

	public void ResetGridState()
	{
		_suppressFilterEvents = true;
		try
		{
			ResetRegionFiltersToAll();
		}
		finally
		{
			_suppressFilterEvents = false;
		}

		SearchText = string.Empty;
		SetAppliedSearchText(string.Empty);

		_sortMemberPath = null;
		_sortDirection = null;
		OnPropertyChanged(nameof(SortMemberPath));
		OnPropertyChanged(nameof(SortDirection));

		ApplyFiltersAndSearch();
		RaiseGridStateChanged();
	}

	private void SetAppliedSearchText(string? text)
	{
		_appliedSearchText = text?.Trim() ?? string.Empty;
	}

	private void BuildFilterOptions()
	{
		var previousState = CaptureRegionFilterState();
		var regions = _allSoftwareItems
			.SelectMany(item => item.Software.Regions)
			.Select(region => region.Acronym)
			.Where(acronym => !string.IsNullOrWhiteSpace(acronym))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(acronym => acronym, StringComparer.OrdinalIgnoreCase);

		ConfigureFilterOptions(RegionFilters, regions, previousState);
	}

	private void ConfigureFilterOptions(
		ObservableCollection<FilterOptionViewModel> target,
		IEnumerable<string> values,
		FilterSelectionState previousState)
	{
		foreach (var option in target)
		{
			option.PropertyChanged -= OnFilterOptionChanged;
		}

		target.Clear();

		var allOption = new FilterOptionViewModel("All", string.Empty, isAllOption: true, isSelected: previousState.AllSelected);
		allOption.PropertyChanged += OnFilterOptionChanged;
		target.Add(allOption);

		foreach (var value in values)
		{
			var isSelected = previousState.AllSelected || previousState.SelectedValues.Contains(value);
			var option = new FilterOptionViewModel(value, value, isAllOption: false, isSelected: isSelected);
			option.PropertyChanged += OnFilterOptionChanged;
			target.Add(option);
		}

		if (!previousState.AllSelected)
		{
			var allSelected = target.Where(option => !option.IsAllOption).All(option => option.IsSelected);
			allOption.IsSelected = allSelected;
		}
	}

	private FilterSelectionState CaptureRegionFilterState()
	{
		if (RegionFilters.Count == 0)
		{
			return new FilterSelectionState(true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
		}

		var allOption = RegionFilters.FirstOrDefault(option => option.IsAllOption);
		var allSelected = allOption?.IsSelected ?? true;
		var selectedValues = GetSelectedFilterValues(RegionFilters);

		return new FilterSelectionState(allSelected, selectedValues);
	}

	private void ApplyRegionFilterState(SoftwareGridState state)
	{
		if (RegionFilters.Count == 0)
		{
			return;
		}

		var selectedRegions = state.SelectedRegions?
			.Where(region => !string.IsNullOrWhiteSpace(region))
			.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var allSelected = state.RegionAllSelected;

		foreach (var option in RegionFilters.Where(option => !option.IsAllOption))
		{
			option.IsSelected = allSelected || selectedRegions.Contains(option.Value);
		}

		var allOption = RegionFilters.FirstOrDefault(option => option.IsAllOption);
		if (allOption != null)
		{
			var shouldSelectAll = allSelected ||
				RegionFilters.Where(option => !option.IsAllOption).All(option => option.IsSelected);
			allOption.IsSelected = shouldSelectAll;
		}
	}

	private void ResetRegionFiltersToAll()
	{
		if (RegionFilters.Count == 0)
		{
			return;
		}

		foreach (var option in RegionFilters.Where(option => !option.IsAllOption))
		{
			option.IsSelected = true;
		}

		var allOption = RegionFilters.FirstOrDefault(option => option.IsAllOption);
		if (allOption != null)
		{
			allOption.IsSelected = true;
		}
	}

	private void OnFilterOptionChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(FilterOptionViewModel.IsSelected) || sender is not FilterOptionViewModel option)
		{
			return;
		}

		if (_isUpdatingFilters || _suppressFilterEvents)
		{
			return;
		}

		_isUpdatingFilters = true;
		try
		{
			if (option.IsAllOption)
			{
				foreach (var item in RegionFilters.Where(item => !item.IsAllOption))
				{
					item.IsSelected = option.IsSelected;
				}
			}
			else
			{
				var allOption = RegionFilters.FirstOrDefault(item => item.IsAllOption);
				if (allOption != null)
				{
					var allSelected = RegionFilters.Where(item => !item.IsAllOption).All(item => item.IsSelected);
					allOption.IsSelected = allSelected;
				}
			}
		}
		finally
		{
			_isUpdatingFilters = false;
		}

		ApplyFiltersAndSearch();
		LogRegionFilterChange();
		RaiseGridStateChanged();
	}

	private void ApplyFiltersAndSearch()
	{
		var queryTokens = Tokenize(_appliedSearchText);
		var regionFilterActive = IsFilterActive(RegionFilters);
		var selectedRegions = GetSelectedFilterValues(RegionFilters);

		var filtered = _allSoftwareItems
			.Where(item =>
				MatchesSearch(item, queryTokens) &&
				MatchesRegionFilters(item, regionFilterActive, selectedRegions))
			.ToList();

		SortSoftwareItems(filtered);
		ReplaceSoftwareItems(filtered);
		UpdateFilterSummary();
	}

	private void SortSoftwareItems(List<SoftwareRowViewModel> items)
	{
		if (string.IsNullOrWhiteSpace(_sortMemberPath) || _sortDirection is null)
		{
			return;
		}

		var direction = _sortDirection == ListSortDirection.Descending ? -1 : 1;
		items.Sort((left, right) =>
		{
			var result = CompareBySortMember(left, right, _sortMemberPath);
			if (result == 0)
			{
				result = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
			}

			return result * direction;
		});
	}

	private static int CompareBySortMember(SoftwareRowViewModel left, SoftwareRowViewModel right, string sortMemberPath)
	{
		return sortMemberPath switch
		{
			"Name" => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name),
			"RegionDisplay" => StringComparer.OrdinalIgnoreCase.Compare(left.RegionDisplay, right.RegionDisplay),
			"Codename" => StringComparer.OrdinalIgnoreCase.Compare(left.Codename, right.Codename),
			"WebVersion" => CompareVersions(left.WebVersion, right.WebVersion),
			"LocalVersion" => CompareVersions(left.LocalVersion, right.LocalVersion),
			"LocalFolderSizeBytes" => CompareNullableLong(left.LocalFolderSizeBytes, right.LocalFolderSizeBytes),
			"UpToDateText" => CompareUpToDate(left, right),
			_ => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name)
		};
	}

	private static int CompareNullableLong(long? left, long? right)
	{
		if (left.HasValue && right.HasValue)
		{
			return left.Value.CompareTo(right.Value);
		}

		if (!left.HasValue && !right.HasValue)
		{
			return 0;
		}

		return left.HasValue ? -1 : 1;
	}

	private static int CompareUpToDate(SoftwareRowViewModel left, SoftwareRowViewModel right)
	{
		return GetUpToDateRank(left).CompareTo(GetUpToDateRank(right));
	}

	private static int GetUpToDateRank(SoftwareRowViewModel item)
	{
		return item.Comparison switch
		{
			VersionComparisonResult.WebNewer => 0,
			VersionComparisonResult.Unknown => 1,
			_ => 2
		};
	}

	private static int CompareVersions(string? left, string? right)
	{
		var leftValue = left ?? string.Empty;
		var rightValue = right ?? string.Empty;

		if (SoftwareVersion.TryParse(leftValue, out var leftVersion) &&
			SoftwareVersion.TryParse(rightValue, out var rightVersion))
		{
			return leftVersion.CompareTo(rightVersion);
		}

		return StringComparer.OrdinalIgnoreCase.Compare(leftValue, rightValue);
	}

	private static string ResolveAppVersion()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(informational))
		{
			return informational;
		}

		var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
		if (!string.IsNullOrWhiteSpace(fileVersion))
		{
			return fileVersion;
		}

		var version = assembly.GetName().Version?.ToString();
		return string.IsNullOrWhiteSpace(version) ? "Unknown" : version;
	}

	private static string GetVersionDisplay(string version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return "Unknown";
		}

		var plusIndex = version.IndexOf('+');
		return plusIndex > 0 ? version[..plusIndex] : version;
	}

	private static string GetBuildMetadata(string version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return string.Empty;
		}

		var plusIndex = version.IndexOf('+');
		if (plusIndex < 0 || plusIndex >= version.Length - 1)
		{
			return string.Empty;
		}

		return version[(plusIndex + 1)..];
	}

	private void UpdateFilterSummary()
	{
		var parts = new List<string>();

		if (!string.IsNullOrWhiteSpace(_appliedSearchText))
		{
			parts.Add($"search \"{_appliedSearchText}\"");
		}

		var regionSummary = BuildRegionSummary();
		if (!string.IsNullOrWhiteSpace(regionSummary))
		{
			parts.Add(regionSummary);
		}

		FilterSummaryText = parts.Count == 0
			? "Filters: none"
			: $"Filters: {string.Join("; ", parts)}";
	}

	private string? BuildRegionSummary()
	{
		if (!IsFilterActive(RegionFilters))
		{
			return null;
		}

		var selected = GetSelectedFilterValues(RegionFilters)
			.OrderBy(region => region, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (selected.Count == 0)
		{
			return "regions: none";
		}

		return $"regions: {string.Join(", ", selected)}";
	}

	private void LogRegionFilterChange()
	{
		if (!IsFilterActive(RegionFilters))
		{
			Logger.Instance.Log("Region filter cleared (all regions).");
			return;
		}

		var selected = GetSelectedFilterValues(RegionFilters)
			.OrderBy(region => region, StringComparer.OrdinalIgnoreCase)
			.ToList();
		var summary = selected.Count == 0 ? "none" : string.Join(", ", selected);
		Logger.Instance.Log($"Region filter applied: {summary}.");
	}

	private void ReplaceSoftwareItems(IReadOnlyList<SoftwareRowViewModel> items)
	{
		SoftwareItems.Clear();
		foreach (var item in items)
		{
			SoftwareItems.Add(item);
		}

		UpdateSelectedCount();
	}

	private static bool IsFilterActive(IEnumerable<FilterOptionViewModel> options)
	{
		return options.Any(option => !option.IsAllOption && !option.IsSelected);
	}

	private static HashSet<string> GetSelectedFilterValues(IEnumerable<FilterOptionViewModel> options)
	{
		return options
			.Where(option => !option.IsAllOption && option.IsSelected)
			.Select(option => option.Value)
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static bool MatchesRegionFilters(
		SoftwareRowViewModel item,
		bool filterActive,
		HashSet<string> selectedRegions)
	{
		if (!filterActive)
		{
			return true;
		}

		if (selectedRegions.Count == 0)
		{
			return false;
		}

		return item.Software.Regions.Any(region => selectedRegions.Contains(region.Acronym));
	}

	private static bool MatchesSearch(SoftwareRowViewModel item, IReadOnlyList<string> queryTokens)
	{
		if (queryTokens.Count == 0)
		{
			return true;
		}

		return ContainsTokensInOrder(Tokenize(item.Name), queryTokens) ||
			ContainsTokensInOrder(Tokenize(item.Codename), queryTokens);
	}

	private static IReadOnlyList<string> Tokenize(string? input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return Array.Empty<string>();
		}

		var builder = new StringBuilder(input.Length);
		foreach (var ch in input)
		{
			builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
		}

		return builder
			.ToString()
			.Split(' ', StringSplitOptions.RemoveEmptyEntries);
	}

	private static bool ContainsTokensInOrder(IReadOnlyList<string> tokens, IReadOnlyList<string> queryTokens)
	{
		if (queryTokens.Count == 0)
		{
			return true;
		}

		if (tokens.Count == 0)
		{
			return false;
		}

		var queryIndex = 0;
		foreach (var token in tokens)
		{
			if (string.Equals(token, queryTokens[queryIndex], StringComparison.OrdinalIgnoreCase))
			{
				queryIndex++;
				if (queryIndex == queryTokens.Count)
				{
					return true;
				}
			}
		}

		return false;
	}

	private void RaiseGridStateChanged()
	{
		if (_suppressGridStateNotifications)
		{
			return;
		}

		GridStateChanged?.Invoke(this, EventArgs.Empty);
	}

	private readonly record struct FilterSelectionState(bool AllSelected, HashSet<string> SelectedValues);

	private void OnSoftwareItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(SoftwareRowViewModel.IsSelected))
		{
			UpdateSelectedCount();
		}
	}

	private void UpdateSelectedCount()
	{
		SelectedSoftwareCount = SoftwareItems.Count(item => item.IsSelected);
	}

	private void OnDownloadManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DownloadManagerViewModel.HasActiveDownloads))
		{
			OnPropertyChanged(nameof(HasActiveDownloads));
			OnPropertyChanged(nameof(CanDownloadSelected));
			OnPropertyChanged(nameof(CanStopDownloads));
		}
	}
}

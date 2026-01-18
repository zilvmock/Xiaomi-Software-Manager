using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using xsm.ViewModels;

namespace xsm.ViewModels.Windows
{
	public sealed class DownloadManagerViewModel : INotifyPropertyChanged
	{
		private bool _hasActiveDownloads;
		private bool _canParallelize;
		private bool _isParallelEnabled = true;
		private int _maxConcurrentDownloads = 2;
		private string _summaryText = string.Empty;
		private string _totalSizeText = "--";
		private string _diskStatsText = "--";
		private string _localFolderSizeText = "--";
		private string _afterDownloadText = "--";
		private string _aggregateSpeedText = "--";

		public ObservableCollection<DownloadItemViewModel> Items { get; } = new();

		public bool HasActiveDownloads
		{
			get => _hasActiveDownloads;
			private set => SetProperty(ref _hasActiveDownloads, value);
		}

		public bool CanParallelize
		{
			get => _canParallelize;
			private set => SetProperty(ref _canParallelize, value);
		}

		public bool IsParallelEnabled
		{
			get => _isParallelEnabled;
			set => SetProperty(ref _isParallelEnabled, value);
		}

		public bool CanAdjustParallelLimit => IsParallelEnabled && CanParallelize;

		public int MaxConcurrentDownloads
		{
			get => _maxConcurrentDownloads;
			set => SetProperty(ref _maxConcurrentDownloads, value);
		}

		public string SummaryText
		{
			get => _summaryText;
			private set => SetProperty(ref _summaryText, value);
		}

		public string TotalSizeText
		{
			get => _totalSizeText;
			private set => SetProperty(ref _totalSizeText, value);
		}

		public string DiskStatsText
		{
			get => _diskStatsText;
			private set => SetProperty(ref _diskStatsText, value);
		}

		public string LocalFolderSizeText
		{
			get => _localFolderSizeText;
			private set => SetProperty(ref _localFolderSizeText, value);
		}

		public string AfterDownloadText
		{
			get => _afterDownloadText;
			private set => SetProperty(ref _afterDownloadText, value);
		}

		public string AggregateSpeedText
		{
			get => _aggregateSpeedText;
			private set => SetProperty(ref _aggregateSpeedText, value);
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public void SetHasActiveDownloads(bool hasActiveDownloads)
		{
			HasActiveDownloads = hasActiveDownloads;
		}

		public void SetCanParallelize(bool canParallelize)
		{
			CanParallelize = canParallelize;
		}

		public void UpdateSummary(string summaryText)
		{
			SummaryText = summaryText;
		}

		public void UpdateTotals(string totalSizeText, string afterDownloadText)
		{
			TotalSizeText = totalSizeText;
			AfterDownloadText = afterDownloadText;
		}

		public void UpdateDiskStats(string diskStatsText, string localFolderSizeText)
		{
			DiskStatsText = diskStatsText;
			LocalFolderSizeText = localFolderSizeText;
		}

		public void UpdateAggregateSpeed(string speedText)
		{
			AggregateSpeedText = speedText;
		}

		private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (Equals(field, value))
			{
				return;
			}

			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			if (propertyName == nameof(IsParallelEnabled) || propertyName == nameof(CanParallelize))
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAdjustParallelLimit)));
			}
		}
	}
}

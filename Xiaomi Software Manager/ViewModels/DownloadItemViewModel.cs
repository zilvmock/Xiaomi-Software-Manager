using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using xsm.Logic.Downloads;
using xsm.Logic.Helpers;

namespace xsm.ViewModels
{
	public sealed class DownloadItemViewModel : INotifyPropertyChanged
	{
		private DownloadStatus _status;
		private long _downloadedBytes;
		private long _totalBytes;
		private double _bytesPerSecond;
		private string _statusText;
		private string _statusColor;
		private bool _canMoveUp;
		private bool _canMoveDown;
		private bool _canCancel;

		public DownloadItemViewModel(string displayName, string version, string fileName)
		{
			DisplayName = displayName;
			Version = version;
			FileName = fileName;
			SummaryFileName = BuildSummaryFileName(fileName);
			_status = DownloadStatus.Queued;
			_statusText = "Queued";
			_statusColor = "#6B7280";
			_canCancel = true;
		}

		public string DisplayName { get; }

		public string Version { get; }

		public string FileName { get; }

		public string SummaryFileName { get; }

		public DownloadStatus Status
		{
			get => _status;
			private set => SetProperty(ref _status, value);
		}

		public string StatusText
		{
			get => _statusText;
			private set => SetProperty(ref _statusText, value);
		}

		public string StatusColor
		{
			get => _statusColor;
			private set => SetProperty(ref _statusColor, value);
		}

		public long DownloadedBytes
		{
			get => _downloadedBytes;
			private set => SetProperty(ref _downloadedBytes, value);
		}

		public long TotalBytes
		{
			get => _totalBytes;
			private set => SetProperty(ref _totalBytes, value);
		}

		public double BytesPerSecond
		{
			get => _bytesPerSecond;
			private set => SetProperty(ref _bytesPerSecond, value);
		}

		public double ProgressPercent => TotalBytes > 0 ? DownloadedBytes * 100d / TotalBytes : 0d;

		public string ProgressText => TotalBytes > 0
			? $"{ByteSizeFormatter.FormatBytes(DownloadedBytes)}/{ByteSizeFormatter.FormatBytes(TotalBytes)}"
			: $"{ByteSizeFormatter.FormatBytes(DownloadedBytes)}/--";

		public string SpeedText => ByteSizeFormatter.FormatBytesPerSecond(BytesPerSecond);

		public bool CanMoveUp
		{
			get => _canMoveUp;
			private set => SetProperty(ref _canMoveUp, value);
		}

		public bool CanMoveDown
		{
			get => _canMoveDown;
			private set => SetProperty(ref _canMoveDown, value);
		}

		public bool CanCancel
		{
			get => _canCancel;
			private set => SetProperty(ref _canCancel, value);
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public void UpdateProgress(long downloadedBytes, long totalBytes, double bytesPerSecond)
		{
			DownloadedBytes = downloadedBytes;
			TotalBytes = totalBytes;
			BytesPerSecond = bytesPerSecond;
			OnPropertyChanged(nameof(ProgressPercent));
			OnPropertyChanged(nameof(ProgressText));
			OnPropertyChanged(nameof(SpeedText));
		}

		public void UpdateStatus(DownloadStatus status)
		{
			Status = status;
			(StatusText, StatusColor, CanCancel) = status switch
			{
				DownloadStatus.Queued => ("Queued", "#6B7280", true),
				DownloadStatus.Downloading => ("Downloading", "#1D4ED8", true),
				DownloadStatus.CheckingMd5 => ("Checking MD5", "#0EA5E9", false),
				DownloadStatus.Completed => ("Done", "#16A34A", false),
				DownloadStatus.Warning => ("Warning", "#B45309", false),
				DownloadStatus.Canceled => ("Canceled", "#9CA3AF", false),
				DownloadStatus.Failed => ("Failed", "#B91C1C", false),
				_ => ("Queued", "#6B7280", true)
			};
		}

		public void UpdateMoveState(bool canMoveUp, bool canMoveDown)
		{
			CanMoveUp = canMoveUp;
			CanMoveDown = canMoveDown;
		}

		private static string BuildSummaryFileName(string fileName)
		{
			var name = Path.GetFileNameWithoutExtension(fileName);
			if (string.IsNullOrWhiteSpace(name))
			{
				return fileName;
			}

			var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 5)
			{
				return string.Join("_", parts.Take(5));
			}

			return name;
		}

		private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (Equals(field, value))
			{
				return;
			}

			field = value;
			OnPropertyChanged(propertyName);
		}

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}

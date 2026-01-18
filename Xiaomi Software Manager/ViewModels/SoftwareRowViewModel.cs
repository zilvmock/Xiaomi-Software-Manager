using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using xsm.Logic.Helpers;
using xsm.Models;

namespace xsm.ViewModels
{
	public sealed class SoftwareRowViewModel : INotifyPropertyChanged
	{
		private bool _isSelected;
		private string _webVersion;
		private string _localVersion;
		private VersionComparisonResult _comparison;
		private long? _localFolderSizeBytes;
		private string _localFolderSizeText = "--";

		public SoftwareRowViewModel(Software software)
		{
			Software = software ?? throw new ArgumentNullException(nameof(software));
			_webVersion = software.WebVersion ?? string.Empty;
			_localVersion = software.LocalVersion ?? string.Empty;
			RegionAcronym = software.Regions.FirstOrDefault()?.Acronym ?? string.Empty;
			UpdateComparison();
		}

		public Software Software { get; }

		public string Name => Software.Name;

		public string RegionDisplay => Software.RegionDisplay;

		public string RegionAcronym { get; }

		public string Codename
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(Software.Codename))
				{
					return Software.Codename;
				}

				return SoftwareLinkParser.TryExtractCodename(Software.WebLink, out var codename)
					? codename
					: string.Empty;
			}
		}

		public string? WebLink => Software.WebLink;

		public string WebVersion
		{
			get => _webVersion;
			private set => SetProperty(ref _webVersion, value);
		}

		public string LocalVersion
		{
			get => _localVersion;
			private set => SetProperty(ref _localVersion, value);
		}

		public long? LocalFolderSizeBytes
		{
			get => _localFolderSizeBytes;
			private set => SetProperty(ref _localFolderSizeBytes, value);
		}

		public string LocalFolderSizeText
		{
			get => _localFolderSizeText;
			private set => SetProperty(ref _localFolderSizeText, value);
		}

		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		public VersionComparisonResult Comparison
		{
			get => _comparison;
			private set => SetProperty(ref _comparison, value);
		}

		public bool IsUpToDate => Comparison == VersionComparisonResult.Equal || Comparison == VersionComparisonResult.LocalNewer;

		public string UpToDateText => IsUpToDate ? "Yes" : "No";

		public string WebVersionColor => ResolveVersionColor(Comparison, isWeb: true);

		public string LocalVersionColor => ResolveVersionColor(Comparison, isWeb: false);

		public string DisplayName => string.IsNullOrWhiteSpace(RegionDisplay) ? Name : $"{Name} {RegionDisplay}";

		public event PropertyChangedEventHandler? PropertyChanged;

		public void UpdateLocalFolderSize(long? sizeBytes)
		{
			LocalFolderSizeBytes = sizeBytes;
			LocalFolderSizeText = sizeBytes.HasValue
				? ByteSizeFormatter.FormatBytes(sizeBytes.Value)
				: "--";
		}

		public void UpdateLocalVersion(string? localVersion)
		{
			var normalized = localVersion ?? string.Empty;
			if (string.Equals(_localVersion, normalized, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			_localVersion = normalized;
			Software.LocalVersion = normalized;
			UpdateComparison();
			OnPropertyChanged(nameof(LocalVersion));
		}

		public void UpdateWebVersion(string? webVersion)
		{
			var normalized = webVersion ?? string.Empty;
			if (string.Equals(_webVersion, normalized, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			_webVersion = normalized;
			Software.WebVersion = normalized;
			UpdateComparison();
			OnPropertyChanged(nameof(WebVersion));
		}

		private void UpdateComparison()
		{
			Comparison = SoftwareVersionComparer.Compare(WebVersion, LocalVersion);
			Software.IsUpToDate = IsUpToDate;
			OnPropertyChanged(nameof(IsUpToDate));
			OnPropertyChanged(nameof(UpToDateText));
			OnPropertyChanged(nameof(WebVersionColor));
			OnPropertyChanged(nameof(LocalVersionColor));
		}

		private static string ResolveVersionColor(VersionComparisonResult comparison, bool isWeb)
		{
			return comparison switch
			{
				VersionComparisonResult.Equal => "#16A34A",
				VersionComparisonResult.WebNewer => isWeb ? "#16A34A" : "#B91C1C",
				VersionComparisonResult.LocalNewer => isWeb ? "#B91C1C" : "#16A34A",
				_ => "#6B7280"
			};
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

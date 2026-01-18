using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Logic;
using xsm.Logic.Downloads;
using xsm.Logic.LocalSoftware;
using xsm.Models;
using xsm.ViewModels;

namespace xsm.UI.Views.Windows
{
	public partial class DownloadManagerWindow : Window
	{
		private bool _allowClose;

		public DownloadManagerWindow()
		{
			InitializeComponent();
			DataContext = DownloadManagerService.Instance.ViewModel;
			Closing += DownloadManagerWindow_OnClosing;
			Opened += DownloadManagerWindow_OnOpened;
			DownloadManagerService.Instance.ViewModel.Items.CollectionChanged += (_, _) =>
				Dispatcher.UIThread.Post(AutoSizeDownloadColumns, DispatcherPriority.Background);
		}

		public void ShowOwnedBy(Window owner)
		{
			DownloadManagerService.Instance.ResetIfIdle();
			Owner = owner;
			Show();
			Activate();
			Dispatcher.UIThread.Post(AutoSizeDownloadColumns, DispatcherPriority.Background);
		}

		public void AllowClose()
		{
			_allowClose = true;
		}

		private void DownloadManagerWindow_OnClosing(object? sender, WindowClosingEventArgs e)
		{
			if (_allowClose || AppLifecycle.Instance.IsShutdownRequested)
			{
				return;
			}

			e.Cancel = true;
			Hide();
		}

		private void DownloadManagerWindow_OnOpened(object? sender, EventArgs e)
		{
			Dispatcher.UIThread.Post(AutoSizeDownloadColumns, DispatcherPriority.Background);
		}

		private void MoveUp_OnClick(object? sender, RoutedEventArgs e)
		{
			if (sender is Button { DataContext: DownloadItemViewModel item })
			{
				DownloadManagerService.Instance.MoveItemUp(item);
			}
		}

		private void MoveDown_OnClick(object? sender, RoutedEventArgs e)
		{
			if (sender is Button { DataContext: DownloadItemViewModel item })
			{
				DownloadManagerService.Instance.MoveItemDown(item);
			}
		}

		private void CancelItem_OnClick(object? sender, RoutedEventArgs e)
		{
			if (sender is Button { DataContext: DownloadItemViewModel item })
			{
				DownloadManagerService.Instance.CancelItem(item);
			}
		}

		private void CancelAll_OnClick(object? sender, RoutedEventArgs e)
		{
			DownloadManagerService.Instance.CancelAll();
		}

		private void DownloadsGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
		{
			if (e.Source is not Control source)
			{
				return;
			}

			var row = source.GetVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
			if (row?.DataContext is not DownloadItemViewModel item)
			{
				return;
			}

			OpenDownloadedFolder(item);
		}

		private void OpenDownloadedFolder(DownloadItemViewModel item)
		{
			var localPath = DownloadManagerService.Instance.LocalSoftwarePath;
			if (string.IsNullOrWhiteSpace(localPath))
			{
				Logger.Instance.Log($"Cannot open folder for {item.DisplayName}. Local software folder is not set.", LogLevel.Error);
				return;
			}

			if (!DownloadManagerService.Instance.TryGetSourceItem(item, out var source))
			{
				Logger.Instance.Log($"Cannot open folder for {item.DisplayName}. Source entry not found.", LogLevel.Error);
				return;
			}

			if (!LocalSoftwareScanner.TryGetModelFolderPath(localPath, source.Name, source.RegionAcronym, out var folderPath))
			{
				Logger.Instance.Log($"Cannot open folder for {item.DisplayName}. Folder not found.", LogLevel.Error);
				return;
			}

			var latestPath = LocalSoftwareScanner.GetPreferredExtractedFolderPath(folderPath, item.Version);
			var targetPath = latestPath ?? folderPath;
			if (latestPath != null)
			{
				Logger.Instance.Log($"Opening folder for latest software of {item.DisplayName}.");
			}
			else
			{
				Logger.Instance.Log($"Opening folder for {item.DisplayName}.");
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
				Logger.Instance.LogException(ex, $"Failed to open folder for {item.DisplayName}.", LogLevel.Error);
			}
		}

		private void AutoSizeDownloadColumns()
		{
			if (DownloadsGrid is null)
			{
				return;
			}

			var items = DownloadManagerService.Instance.ViewModel.Items;
			if (items.Count == 0)
			{
				return;
			}

			const double cellPadding = 28;
			const double headerPadding = 40;

			var fontFamily = TextElement.GetFontFamily(DownloadsGrid);
			var fontSize = TextElement.GetFontSize(DownloadsGrid);
			var fontStyle = TextElement.GetFontStyle(DownloadsGrid);
			var fontWeight = TextElement.GetFontWeight(DownloadsGrid);
			var fontStretch = TextElement.GetFontStretch(DownloadsGrid);
			var headerFontWeight = FontWeight.SemiBold;

			foreach (var column in DownloadsGrid.Columns)
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

				if (boundColumn.Binding is Avalonia.Data.Binding binding && !string.IsNullOrWhiteSpace(binding.Path))
				{
					foreach (var item in items)
					{
						var value = GetBindingValue(item, binding.Path);
						var text = value?.ToString() ?? string.Empty;
						var width = MeasureTextWidth(text, fontFamily, fontSize, fontStyle, fontWeight, fontStretch) + cellPadding;
						if (width > maxWidth)
						{
							maxWidth = width;
						}
					}

					if (string.Equals(binding.Path, nameof(DownloadItemViewModel.DisplayName), StringComparison.Ordinal))
					{
						maxWidth = Math.Min(maxWidth, 210);
					}
					else if (string.Equals(binding.Path, nameof(DownloadItemViewModel.Version), StringComparison.Ordinal))
					{
						maxWidth = Math.Min(maxWidth, 220);
					}
				}

				if (column.MinWidth > 0)
				{
					maxWidth = Math.Max(maxWidth, column.MinWidth);
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
}

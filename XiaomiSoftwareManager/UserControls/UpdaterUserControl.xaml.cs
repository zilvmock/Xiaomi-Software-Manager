using System.Windows;
using System.Windows.Controls;

namespace XiaomiSoftwareManager.UserControls
{
	public partial class UpdaterUserControl : UserControl
	{
		public UpdaterUserControl()
		{
			InitializeComponent();
			Results.Visibility = Visibility.Collapsed;
			DownloadPanel.Visibility = Visibility.Collapsed;
		}

		public void ShowResults(string results, string updateMessage = "")
		{
			Status.Text = results;

			if (!string.IsNullOrEmpty(updateMessage))
			{
				Results.Visibility = Visibility.Visible;
				Results.Text = updateMessage;
			}
		}

		public void OnDownloadSpeedChanged(string status)
		{
			Dispatcher.Invoke(() =>
			{
				DownloadSpeed.Text = status;
			});
		}

		public void OnDownloadSizeChanged(string status)
		{
			Dispatcher.Invoke(() =>
			{
				DownloadSize.Text = status;
			});
		}

		public void OnDownloadPercentChanged(int progress)
		{
			Dispatcher.Invoke(() =>
			{
				DownloadProgressBar.Value = progress;
				DownloadPercent.Text = $"{progress}%";
			});
		}

		public void OnDownloadStarted()
		{
			Dispatcher.Invoke(() =>
			{
				DownloadPanel.Visibility = Visibility.Visible;
			});
		}
	}
}

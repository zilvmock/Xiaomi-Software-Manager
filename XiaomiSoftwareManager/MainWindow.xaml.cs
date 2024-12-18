using Newtonsoft.Json;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using XiaomiSoftwareManager.Dialogs;
using XiaomiSoftwareManager.DownloadManager;
using XiaomiSoftwareManager.Models;
using XiaomiSoftwareManager.UserControls;

namespace XiaomiSoftwareManager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		// Constants and P/Invoke for resizing
		private const int WM_SYSCOMMAND = 0x112;
		private const int SC_SIZE = 0xF000;

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		public enum WindowResizeEdge // Indicates which edge of the window is being resized.
		{
			Left = 1,
			Right = 2,
			Top = 3,
			TopLeft = 4,
			TopRight = 5,
			Bottom = 6,
			BottomLeft = 7,
			BottomRight = 8
		}

		private AppInfo appInfo;

		public MainWindow()
		{
			InitializeUpdaterPipe();
			InitializeComponent();
			LoadInfo();
		}

		private static Task InitializeUpdaterPipe()
		{
			return Task.Run(() =>
			{
				using var server = new NamedPipeServerStream("XSM");
				server.WaitForConnection();

				using var reader = new StreamReader(server);
				string message = reader.ReadLine() ?? throw new NullReferenceException();
				if (message == "shutdown")
				{
					Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
				}
			});
		}

		private void LoadInfo()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			string resourceName = "XiaomiSoftwareManager.info.json";

			using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
			using StreamReader reader = new(stream);
			string json = reader.ReadToEnd();
			appInfo = JsonConvert.DeserializeObject<AppInfo>(json)!;
		}
		private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				DragResize(WindowResizeEdge.BottomRight);
			}
		}

		private void DragResize(WindowResizeEdge edge)
		{
			// Call the Windows API function to initiate resizing.
			SendMessage(new WindowInteropHelper(this).Handle, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + edge), IntPtr.Zero);
		}

		private void AboutMenu_Click(object sender, RoutedEventArgs e)
		{
			AboutUserControl aboutControl = new($"{appInfo.Title}\n{appInfo.Version}\n\nDeveloped by {appInfo.Author}");
			new CustomDialog(aboutControl, "About", CustomDialog.DialogType.OK).ShowDialog();
		}

		private async void UpdateMenu_Click(object sender, RoutedEventArgs e)
		{
			UpdaterUserControl updaterControl = new();
			CustomDialog updateDialog = new(updaterControl, "Updates", CustomDialog.DialogType.OK);
			updateDialog.ToggleButtonsEnabled();
			updateDialog.Show();

			Updater updater = new();
			// TODO: Prideti prie settingu ar gauti pre releases ar ne. Jei gauti: GetLatestReleaseAsync(true)
			GitHubRelease? release = await updater.GetLatestReleaseAsync(new HttpClient());

			if (release == null)
			{
				updaterControl.ShowResults("Can't check it right now...");
				updateDialog.ToggleButtonsEnabled();
				return;
			}

			if (Updater.UpdateIsAvailable(appInfo.Version, release.TagName))
			{
				updaterControl.ShowResults("Update Available", $"{appInfo.Version} -> {release.TagName}\nDo you want update now?");
				updateDialog.ChangeButtons(CustomDialog.DialogType.YesNo);
				updateDialog.ToggleButtonsEnabled();

				updater.DownloadSpeedChanged += updaterControl.OnDownloadSpeedChanged;
				updater.DownloadSizeChanged += updaterControl.OnDownloadSizeChanged;
				updater.DownloadPercentChanged += updaterControl.OnDownloadPercentChanged;
				updater.DownloadStarted += updaterControl.OnDownloadStarted;

				updateDialog.DisableCloseBehaviourFirstButton();
				//(_, __) is a shorthand for (object sender, RoutedEventArgs e) when you don't need to use the sender or event args.
				updateDialog.FirstButton.Click += async (_, __) =>
				{
					updaterControl.ShowResults("Downloading...", $"{appInfo.Version} -> {release.TagName}");
					updateDialog.ToggleButtonsEnabled();
					Task<string> downloadTask = Task.Run(async () => await updater.DownloadUpdateAsync(release));
					string filePath = await downloadTask;

					if (downloadTask.IsCompletedSuccessfully && !string.IsNullOrEmpty(filePath))
					{
						updaterControl.ShowResults("Installing", $"{appInfo.Version} -> {release.TagName}\nApplication will restart now...");
						updaterControl.DownloadPanel.Visibility = Visibility.Collapsed;
						try
						{
							updater.InstallUpdate(filePath);
						}
						catch (Exception ex)
						{
							MessageBox.Show($"Error while attempting to update: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						}
					}
				};
			}
			else
			{
				updaterControl.ShowResults("Up to Date!", $"{appInfo.Version} === {release.TagName}");
			}

		}
	}
}
using Avalonia;
using Avalonia.Controls;
using System;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using xsm;
using xsm.Logic;
using xsm.UI.Views.Windows;
using xsm.ViewModels.Windows;

namespace xsm.tests.app;

public sealed class AppStartupTests
{
	[AvaloniaTest]
	public void App_IsInitialized()
	{
		Assert.That(Application.Current, Is.TypeOf<App>());
	}

	[AvaloniaTest]
	public void MainWindow_LoadsXamlAndViewModel()
	{
		var window = new MainWindow();

		Assert.That(window.DataContext, Is.TypeOf<MainWindowViewModel>());
		Assert.That(window.FindControl<DataGrid>("SoftwareGrid"), Is.Not.Null);
	}

	[AvaloniaTest]
	public async Task MainWindow_CloseRequestsShutdown()
	{
		var window = new MainWindow();
		var closedTcs = new TaskCompletionSource<bool>();
		window.Closed += (_, _) => closedTcs.TrySetResult(true);

		window.Close();

		var completed = await Task.WhenAny(closedTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.That(completed, Is.EqualTo(closedTcs.Task));
		Assert.That(AppLifecycle.Instance.IsShutdownRequested, Is.True);
	}
}

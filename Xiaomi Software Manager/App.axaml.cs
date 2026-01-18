using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Logic;
using xsm.Models;
using xsm.UI.Views.Windows;

namespace xsm;

public partial class App : Application
{
	private const string DisableDevToolsEnvVar = "XSM_DISABLE_DEVTOOLS";

	public App()
	{
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		Dispatcher.UIThread.UnhandledException += OnUiUnhandledException;
	}

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
#if DEBUG
		if (ShouldAttachDeveloperTools())
		{
			this.AttachDeveloperTools();
		}
#endif
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
			desktop.Exit += OnDesktopExit;
		}

		LogApplicationStarted();

		base.OnFrameworkInitializationCompleted();
	}

	private async void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs args)
	{
		Logger.Instance.Log("Application exiting.");
		try
		{
			await AppLifecycle.Instance.ShutdownAsync(TimeSpan.FromSeconds(5));
		}
		finally
		{
			Logger.Instance.Shutdown();
		}
	}

	private static void LogApplicationStarted()
	{
		Logger.Instance.Log("Application started.", LogLevel.Info, details: new[]
		{
			new LogEntry("Base directory", AppContext.BaseDirectory, level: LogLevel.Debug),
			new LogEntry("Runtime", RuntimeInformation.FrameworkDescription, level: LogLevel.Debug),
			new LogEntry("OS", RuntimeInformation.OSDescription, level: LogLevel.Debug),
			new LogEntry("Architecture", RuntimeInformation.OSArchitecture.ToString(), level: LogLevel.Debug)
		});
	}

	private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
	{
		if (args.ExceptionObject is Exception exception)
		{
			Logger.Instance.LogException(exception, "Unhandled exception (AppDomain).", LogLevel.Error);
		}
		else
		{
			Logger.Instance.Log("Unhandled exception (AppDomain) with non-exception payload.", LogLevel.Error);
		}

		AppLifecycle.Instance.PerformEmergencyCleanup();
		Logger.Instance.Shutdown();
	}

	private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
	{
		Logger.Instance.LogException(args.Exception, "Unobserved task exception.", LogLevel.Error);
		args.SetObserved();
	}

	private static void OnProcessExit(object? sender, EventArgs args)
	{
		Logger.Instance.Log("Process exit.", LogLevel.Info);
		AppLifecycle.Instance.PerformEmergencyCleanup();
		Logger.Instance.Shutdown();
	}

	private static void OnUiUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs args)
	{
		Logger.Instance.LogException(args.Exception, "Unhandled UI exception.", LogLevel.Error);
		AppLifecycle.Instance.PerformEmergencyCleanup();
		Logger.Instance.Shutdown();
	}

	private static bool ShouldAttachDeveloperTools()
	{
		var disableDevTools = Environment.GetEnvironmentVariable(DisableDevToolsEnvVar);
		if (string.IsNullOrWhiteSpace(disableDevTools))
		{
			return true;
		}

		return !(disableDevTools.Equals("1", StringComparison.OrdinalIgnoreCase)
			|| disableDevTools.Equals("true", StringComparison.OrdinalIgnoreCase));
	}
}

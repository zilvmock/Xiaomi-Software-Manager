using Avalonia;
using System;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Models;

namespace xsm;

class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args)
	{
		Logger.Initialize();

		using var instanceGuard = SingleInstanceGuard.TryAcquire("xsm.single-instance");
		if (instanceGuard == null)
		{
			Logger.Instance.Log("Another instance is already running.", LogLevel.Warning);
			return;
		}

		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
}

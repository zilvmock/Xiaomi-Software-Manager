using System;
using Avalonia;
using Avalonia.Headless;
using xsm;

[assembly: AvaloniaTestApplication(typeof(xsm.tests.app.TestAppBuilder))]

namespace xsm.tests.app;

public static class TestAppBuilder
{
	static TestAppBuilder()
	{
		Environment.SetEnvironmentVariable("XSM_DISABLE_DEVTOOLS", "1");
	}

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UseHeadless(new AvaloniaHeadlessPlatformOptions())
			.WithInterFont();
}

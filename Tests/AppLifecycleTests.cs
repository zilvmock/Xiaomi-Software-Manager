using System;
using System.Threading.Tasks;
using NUnit.Framework;
using xsm.Logic;

namespace xsm.Tests;

[NonParallelizable]
public sealed class AppLifecycleTests
{
	[Test]
	public async Task Shutdown_CompletesAndSignalsToken()
	{
		await AppLifecycle.Instance.ShutdownAsync(TimeSpan.FromSeconds(2));

		Assert.That(AppLifecycle.Instance.IsShutdownRequested, Is.True);
		Assert.That(AppLifecycle.Instance.ShutdownToken.IsCancellationRequested, Is.True);
	}
}

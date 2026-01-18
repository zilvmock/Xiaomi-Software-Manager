using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using xsm.updater;

namespace xsm.tests.updater;

public sealed class UpdaterCliTests
{
	[Test]
	public void Main_ReturnsError_WhenArgumentsMissing()
	{
		var exitCode = Program.Main(Array.Empty<string>());

		Assert.That(exitCode, Is.EqualTo(1));
	}

	[Test]
	public void Main_ReturnsError_WhenZipMissing()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"xsm-test-{Guid.NewGuid():N}");
		var exePath = Path.Combine(tempDir, "xsm.exe");
		Directory.CreateDirectory(tempDir);

		try
		{
			var exitCode = Program.Main(new[]
			{
				"999999",
				exePath,
				Path.Combine(tempDir, "missing.zip")
			});

			Assert.That(exitCode, Is.EqualTo(1));
		}
		finally
		{
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	[Test]
	public void ShouldSkip_SkipsProtectedPaths()
	{
		var method = typeof(Program).GetMethod(
			"ShouldSkip",
			BindingFlags.NonPublic | BindingFlags.Static);
		Assert.That(method, Is.Not.Null);

		var skipData = (bool)method!.Invoke(null, new object[] { "data\\xsm.db" })!;
		var skipLogs = (bool)method.Invoke(null, new object[] { "logs\\app.log" })!;
		var skipDrivers = (bool)method.Invoke(null, new object[] { "drivers\\chromedriver.exe" })!;
		var skipUpdater = (bool)method.Invoke(null, new object[] { "xsm.updater.exe" })!;
		var skipApp = (bool)method.Invoke(null, new object[] { "xsm.exe" })!;

		Assert.That(skipData, Is.True);
		Assert.That(skipLogs, Is.True);
		Assert.That(skipDrivers, Is.True);
		Assert.That(skipUpdater, Is.True);
		Assert.That(skipApp, Is.False);
	}
}

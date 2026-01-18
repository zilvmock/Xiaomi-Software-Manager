using System.Diagnostics;
using System.IO.Compression;

namespace xsm.updater;

public static class Program
{
	private const string UpdaterFileName = "xsm.updater.exe";
	private static readonly string[] ProtectedTopLevelDirectories =
	{
		"data",
		"logs",
		"drivers"
	};

	public static int Main(string[] args)
	{
		if (args.Length != 3)
		{
			Console.WriteLine("Usage: xsm.updater <pid> <exePath> <zipPath>");
			return 1;
		}

		if (!int.TryParse(args[0], out var processId))
		{
			Console.WriteLine("[ERROR] Invalid process id.");
			return 1;
		}

		var mainExecutablePath = args[1];
		var zipPath = args[2];

		if (string.IsNullOrWhiteSpace(mainExecutablePath))
		{
			Console.WriteLine("[ERROR] Executable path is missing.");
			return 1;
		}

		if (!File.Exists(zipPath))
		{
			Console.WriteLine($"[ERROR] Update package not found: {zipPath}");
			return 1;
		}

		var targetDirectory = Path.GetDirectoryName(mainExecutablePath);
		if (string.IsNullOrWhiteSpace(targetDirectory))
		{
			Console.WriteLine("[ERROR] Unable to resolve target directory.");
			return 1;
		}

		string? extractPath = null;
		var updateSucceeded = false;
		try
		{
			Console.WriteLine("# Waiting for application to exit...");
			if (!WaitForProcessExit(processId, TimeSpan.FromSeconds(30)))
			{
				Console.WriteLine("[ERROR] Application did not exit in time.");
				return 1;
			}

			extractPath = Path.Combine(Path.GetTempPath(), $"xsm-update-{Guid.NewGuid():N}");
			Directory.CreateDirectory(extractPath);
			Console.WriteLine($"# Extracting update to {extractPath}");
			ZipFile.ExtractToDirectory(zipPath, extractPath);

			Console.WriteLine("# Applying update...");
			CopyUpdateFiles(extractPath, targetDirectory);
			updateSucceeded = true;

			Console.WriteLine("# Relaunching application...");
			Process.Start(new ProcessStartInfo
			{
				FileName = mainExecutablePath,
				WorkingDirectory = targetDirectory,
				UseShellExecute = true
			});

			return 0;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[ERROR] Update failed: {ex.Message}");
			return 1;
		}
		finally
		{
			if (!string.IsNullOrWhiteSpace(extractPath))
			{
				TryDeleteDirectory(extractPath);
			}

			if (updateSucceeded)
			{
				TryDeleteFile(zipPath);
			}
		}
	}

	private static bool WaitForProcessExit(int processId, TimeSpan timeout)
	{
		try
		{
			using var process = Process.GetProcessById(processId);
			if (process.HasExited)
			{
				return true;
			}

			return process.WaitForExit((int)timeout.TotalMilliseconds);
		}
		catch (ArgumentException)
		{
			return true;
		}
	}

	private static void CopyUpdateFiles(string sourceDirectory, string targetDirectory)
	{
		foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
			if (ShouldSkip(relativePath))
			{
				continue;
			}

			var destinationPath = Path.Combine(targetDirectory, relativePath);
			var destinationDirectory = Path.GetDirectoryName(destinationPath);
			if (!string.IsNullOrWhiteSpace(destinationDirectory))
			{
				Directory.CreateDirectory(destinationDirectory);
			}

			File.Copy(filePath, destinationPath, true);
			Console.WriteLine($"- {relativePath}");
		}
	}

	private static bool ShouldSkip(string relativePath)
	{
		var fileName = Path.GetFileName(relativePath);
		if (string.Equals(fileName, UpdaterFileName, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
		var parts = normalized.Split(Path.DirectorySeparatorChar, 2, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
		{
			return false;
		}

		return ProtectedTopLevelDirectories.Any(entry =>
			string.Equals(entry, parts[0], StringComparison.OrdinalIgnoreCase));
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, true);
			}
		}
		catch
		{
		}
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}
}

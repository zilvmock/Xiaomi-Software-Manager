using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipes;

namespace UpdaterApp
{
	public class Program
	{
		private static CancellationToken cancellationToken;
		public static void CallMain(string[] args, CancellationToken token = default)
		{
			cancellationToken = token;
			Main(args);
		}

		static void Main(string[] args)
		{
			//if (!Debugger.IsAttached)
			//{
			//    Debugger.Launch();
			//}

			if (args.Length != 4)
			{
				Console.WriteLine("[ERROR] Invalid arguments. Cannot continue...");
				return;
			}

			if (!int.TryParse(args[0], out int mainProcessId))
			{
				Console.WriteLine("[ERROR] Failed to parse process ID. Cannot continue...");
				return;
			}

			string mainProcessName = args[1];
			string mainExecutablePath = args[2];
			string updateZipPath = args[3];
			string updateFolder = "update";

			try
			{
				string executableDirectory = Path.GetDirectoryName(mainExecutablePath)!;
				string extractionPath = Path.Combine(executableDirectory, updateFolder);

				if (Directory.Exists(extractionPath))
					Directory.Delete(extractionPath, true);

				Console.WriteLine("# Checking if application is closed...");

				try
				{
					/*
					 * The main application should call to quit itself.
					 * - If it doesn't happen, it will attempt to quit using a NamedPipe.
					 */
					Process process = Process.GetProcessById(mainProcessId);

					if (!process.HasExited &&
						process.ProcessName.Equals(mainProcessName, StringComparison.OrdinalIgnoreCase) &&
						process.MainModule?.FileName == mainExecutablePath)
					{
						Console.WriteLine("[!] Main application is still running. Atempting to quit...");
						try
						{
							using NamedPipeClientStream client = new("XSM");
							client.Connect();
							using StreamWriter writer = new(client);
							writer.WriteLine("shutdown");
							writer.Flush();

							int checkAttempts = 1;
							while (checkAttempts <= 5)
							{
								Console.WriteLine($"- ({checkAttempts}) Waiting to quit...");
								Process.GetProcessById(mainProcessId);
								checkAttempts++;
								Thread.Sleep(1000);
							}
						}
						catch (ArgumentException) /* continue: throws when process isn't found - the main application has exited. */
						{
							if (cancellationToken.IsCancellationRequested) { return; }
						}
						catch (Exception ex)
						{
							Console.WriteLine($"[ERROR] Error during update. Reason:\n{ex.Message}");
							//Console.ReadLine();
							//TODO: i loga issaugoti fail, nestabdyti cia.
							//laikinai:
							Thread.Sleep(3000);
							return;
						}
					}

					Console.WriteLine("# Main application has exited. Continuing...");
				}
				catch (ArgumentException) { Console.WriteLine("# Main application process not found. Continuing..."); }

				Console.WriteLine("# Updating...");

				ZipFile.ExtractToDirectory(updateZipPath, extractionPath);
				string[] extractedFiles = Directory.GetFiles(extractionPath, "*", SearchOption.AllDirectories);

				foreach (string file in extractedFiles)
				{
					string destFile = Path.Combine(executableDirectory, Path.GetFileName(file));
					File.Copy(file, destFile, true);
					Console.WriteLine($" - Copying {Path.GetFileName(file)}...");
				}

				Console.WriteLine("# Update completed successfully.");

				Process.Start(new ProcessStartInfo
				{
					FileName = mainExecutablePath,
					UseShellExecute = false,
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ERROR] Error during update. Reason:\n{ex.Message}");
				//Console.ReadLine();
				//TODO: i loga issaugoti fail, nestabdyti cia.
				//laikinai:
				Thread.Sleep(3000);
				return;
			}
			finally
			{
				string executableDirectory = Path.GetDirectoryName(mainExecutablePath)!;
				string extractionPath = Path.Combine(executableDirectory, updateFolder);

				if (Directory.Exists(extractionPath)) { Directory.Delete(extractionPath, true); }
				if (File.Exists(executableDirectory)) { File.Delete(extractionPath); }
			}
		}
	}
}

using System.Diagnostics;
using Tests.Helpers;
using System.IO.Pipes;

namespace Tests.UpdaterTests
{
    public class UpdaterConsoleAppTests
    {
        [Fact]
        public void Main_InvalidArguments_ShowsErrorMessage()
        {
            // Arrange
            string[] args = ["invalid"]; // Fewer than 4 arguments
            using var output = new StringWriter();
            Console.SetOut(output);

            // Act
            Updater.Program.CallMain(args);

            // Assert
            string consoleOutput = output.ToString();
            Assert.Contains("[ERROR] Invalid arguments", consoleOutput);
        }

        [Fact]
        public void Main_InvalidProcessId_ShowsErrorMessage()
        {
            // Arrange
            string[] args = ["invalid", "mainApp", "path", "update.zip"];
            using var output = new StringWriter();
            Console.SetOut(output);

            // Act
            Updater.Program.CallMain(args);

            // Assert
            string consoleOutput = output.ToString();
            Assert.Contains("[ERROR] Failed to parse process ID", consoleOutput);
        }

        [Fact]
        public void Main_ExtractsAndReplacesFilesCorrectly()
        {
            // Arrange
            using var tempDirectoryManager = new TempDirectoryManager();
            Directory.CreateDirectory(tempDirectoryManager.TempDirectory);
            var zipContents = new Dictionary<string, string>
            {
                { "test.txt", "New Content!" }
            };
            string testZipPath = tempDirectoryManager.CreateTestZipFile("test.zip", zipContents);
            string extractionDir = Path.Combine(tempDirectoryManager.TempDirectory, "update");
            string[] args = ["9999999", "mainApp", Path.Combine(tempDirectoryManager.TempDirectory, "Updater.exe"), testZipPath];
            
            // Since process is not found it skips to the extraction.

            string oldContent = "Old Content.";
            string tempFilePath = Path.Combine(tempDirectoryManager.TempDirectory, "test.txt");
            File.WriteAllText(tempFilePath, oldContent);

            // Act
            Updater.Program.CallMain(args);

            // Assert
            string updatedContent = File.ReadAllText(Path.Combine(tempDirectoryManager.TempDirectory, "test.txt"));
            Assert.NotEqual(updatedContent, oldContent);
        }

        [Fact]
        public async Task Main_SendsShutdownSignalToNamedPipeServer()
        {
            // Arrange
            using var tempDirectoryManager = new TempDirectoryManager();
            var dummyProcessStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C timeout /T 10 > nul", // Dummy process that waits for 10 seconds
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var dummyProcess = Process.Start(dummyProcessStartInfo)!;

            string dummyProcessPath = dummyProcess.MainModule!.FileName!;
            string testZipPath = tempDirectoryManager.CreateTestZipFile();

            string[] args = {
                dummyProcess.Id.ToString(),
                dummyProcess.ProcessName,
                dummyProcessPath,
                testZipPath
            };

            bool shutdownSignalReceived = false;

            using var namedPipeServer = new NamedPipeServerStream("XSM", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            Task pipeTask = Task.Run(async () =>
            {
                await namedPipeServer.WaitForConnectionAsync();

                using var reader = new StreamReader(namedPipeServer);
                string? command = await reader.ReadLineAsync();

                if (command == "shutdown")
                {
                    shutdownSignalReceived = true;
                }
            });

            // Act
            Task programTask = Task.Run(() => { Updater.Program.CallMain(args); });
            await Task.WhenAny(programTask, Task.Delay(5000)); // Ensure the test doesn't hang indefinitely

            // Assert
            Assert.True(shutdownSignalReceived, "Shutdown signal was not received via the named pipe.");

            // Ensure the dummy process is killed for cleanup
            if (!dummyProcess.HasExited) { dummyProcess.Kill(); }
        }
    }
}
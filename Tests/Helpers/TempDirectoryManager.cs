using System.IO.Compression;

namespace Tests.Helpers
{
    public class TempDirectoryManager : IDisposable
    {
        public string TempDirectory { get; private set; }

        public TempDirectoryManager()
        {
            TempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(TempDirectory);
        }

        /// <summary>
        /// Creates a test zip file inside the temporary directory with specified contents.
        /// </summary>
        /// <param name="fileName">Name of the zip file to create.</param>
        /// <param name="contents">Dictionary where the key is the file name and value is the content.</param>
        /// <returns>Path to the created zip file.</returns>
        public string CreateTestZipFile(string fileName = "test.zip", Dictionary<string, string>? contents = null)
        {
            contents ??= new Dictionary<string, string>
            {
                { "test.txt", "Test content" }
            };

            string tempDir = Path.Combine(TempDirectory, "temp");
            Directory.CreateDirectory(tempDir);

            foreach (var file in contents)
            {
                File.WriteAllText(Path.Combine(tempDir, file.Key), file.Value);
            }

            string zipPath = Path.Combine(TempDirectory, fileName);
            ZipFile.CreateFromDirectory(tempDir, zipPath);
            Directory.Delete(tempDir, true);

            return zipPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(TempDirectory))
            {
                Directory.Delete(TempDirectory, true);
            }
        }
    }
}

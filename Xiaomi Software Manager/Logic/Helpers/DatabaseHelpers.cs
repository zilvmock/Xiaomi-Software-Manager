using System;
using System.IO;
using System.Threading.Tasks;
using xsm.Data;
using xsm.Data.Interfaces;
using xsm.Logic.LocalSoftware;

namespace xsm.Logic.Helpers
{
	public class DatabaseHelpers
	{
		private readonly IFolderSourceRepository _folderSourceRepository;
		private readonly ISoftwareRepository _softwareRepository;

		public DatabaseHelpers(IFolderSourceRepository folderSourceRepository, ISoftwareRepository softwareRepository)
		{
			_folderSourceRepository = folderSourceRepository;
			_softwareRepository = softwareRepository;
		}

		// TODO: Add a test for this to GeneralDatabaseTests
		// Functionality haven't been tested yet
		public async Task UpdateLocalVersionAsync(string name, string regionAcronym, string version = "")
		{
			if (!string.IsNullOrWhiteSpace(version))
			{
				await UpdateSoftwareStateAsync(name, regionAcronym, version);
				return;
			}

			var localSoftwareFolderSource = await _folderSourceRepository.GetByNameAsync(FolderSourceDefaults.LocalSoftwareName);
			var localSoftwarePath = localSoftwareFolderSource?.Path;

			if (string.IsNullOrWhiteSpace(localSoftwarePath) || !Directory.Exists(localSoftwarePath))
			{
				await UpdateSoftwareStateAsync(name, regionAcronym, string.Empty);
				return;
			}

			if (!LocalSoftwareScanner.TryGetModelFolderPath(localSoftwarePath, name, regionAcronym, out var modelFolderPath))
			{
				await UpdateSoftwareStateAsync(name, regionAcronym, string.Empty);
				return;
			}

			var latestLocalVersion = LocalSoftwareScanner.GetLatestVersionInModelFolder(modelFolderPath);
			await UpdateSoftwareStateAsync(name, regionAcronym, latestLocalVersion);
		}

		private async Task UpdateSoftwareStateAsync(string name, string regionAcronym, string latestLocalVersion)
		{
			var software = await _softwareRepository.GetByNameAndRegionAcronymAsync(name, regionAcronym);
			if (software == null)
			{
				return;
			}

			software.LocalVersion = latestLocalVersion;
			software.IsUpToDate = IsUpToDate(software.WebVersion, latestLocalVersion);
			await _softwareRepository.UpdateAsync(software);
		}

		private static bool IsUpToDate(string? webVersion, string? localVersion)
		{
			return SoftwareVersionComparer.IsUpToDate(webVersion, localVersion);
		}
	}
}

using System.Threading.Tasks;
using xsm.Data.Interfaces;
using xsm.Models;

namespace xsm.Data.Services
{
	public class SoftwareService
	{
		private readonly ISoftwareRepository _softwareRepository;
		private readonly IRegionRepository _regionRepository;

		public SoftwareService(ISoftwareRepository softwareRepository, IRegionRepository regionRepository)
		{
			_softwareRepository = softwareRepository;
			_regionRepository = regionRepository;
		}

		public async Task AddSoftwareWithRegionAsync(Software software, string regionName)
		{
			var addedSoftware = await _softwareRepository.AddAsync(software);
			var region = await _regionRepository.GetRegionByNameAsync(regionName);

			if (region != null)
			{
				await _softwareRepository.AssignRegionToSoftwareAsync(addedSoftware.Id, region.Id);
			}
		}
	}
}

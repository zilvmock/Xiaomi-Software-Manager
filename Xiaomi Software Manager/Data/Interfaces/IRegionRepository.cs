using System.Collections.Generic;
using System.Threading.Tasks;
using xsm.Models;

namespace xsm.Data.Interfaces
{
	public interface IRegionRepository
	{
		Dictionary<string, string> RegionRef { get; }
		Task<List<Region>> GetAllAsync();
		Task<Region?> GetRegionByNameAsync(string regionName);
		Task<Region?> GetByIdAsync(int id);
		Task<Region> AddAsync(Region region);
		Task UpdateAsync(Region region);
		Task DeleteAsync(int id);
	}
}
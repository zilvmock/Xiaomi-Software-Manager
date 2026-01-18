using System.Collections.Generic;
using System.Threading.Tasks;
using xsm.Models;

namespace xsm.Data.Interfaces
{
	public interface ISoftwareRepository
	{
		Task<List<Software>> GetAllAsync();
		Task<Software?> GetByIdAsync(int id);
		Task<Software?> GetByNameAndRegionAcronymAsync(string modelName, string regionAcronym);
		Task AssignRegionToSoftwareAsync(int id1, int id2);
		Task<Software> AddAsync(Software software);
		Task UpdateAsync(Software software);
		Task DeleteAsync(int id);
	}
}
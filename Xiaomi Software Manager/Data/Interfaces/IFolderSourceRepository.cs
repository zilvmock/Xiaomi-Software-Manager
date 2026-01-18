using System.Collections.Generic;
using System.Threading.Tasks;
using xsm.Models;

namespace xsm.Data.Interfaces
{
	public interface IFolderSourceRepository
	{
		Task<List<FolderSource>> GetAllAsync();
		Task<FolderSource?> GetByIdAsync(int id);
		Task<FolderSource?> GetByNameAsync(string name);
		Task<FolderSource> AddAsync(FolderSource folderSource);
		Task UpdateAsync(FolderSource folderSource);
		Task DeleteAsync(int id);
	}
}

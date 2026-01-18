using System.Collections.Generic;
using System.Threading.Tasks;
using xsm.Models;

namespace xsm.Data.Interfaces;

public interface IDownloadDomainRepository
{
	Task<List<DownloadDomain>> GetAllAsync();
	Task<DownloadDomain?> GetByDomainAsync(string domain);
	Task AddAsync(DownloadDomain downloadDomain);
	Task UpdateAsync(DownloadDomain downloadDomain);
	Task RemoveRangeAsync(IEnumerable<DownloadDomain> downloadDomains);
}

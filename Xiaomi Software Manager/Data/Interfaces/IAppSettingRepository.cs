using System.Threading.Tasks;
using xsm.Models;

namespace xsm.Data.Interfaces;

public interface IAppSettingRepository
{
	Task<AppSetting?> GetByKeyAsync(string key);
	Task SetAsync(string key, string? value);
}

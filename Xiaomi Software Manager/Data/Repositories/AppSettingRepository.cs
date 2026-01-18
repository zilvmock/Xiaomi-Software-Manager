using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data.Interfaces;
using xsm.Models;

namespace xsm.Data.Repositories;

public sealed class AppSettingRepository : IAppSettingRepository
{
	private readonly AppDbContext _context;

	public AppSettingRepository(AppDbContext context)
	{
		_context = context;
	}

	public async Task<AppSetting?> GetByKeyAsync(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Setting key cannot be null or empty.", nameof(key));
		}

		return await _context.AppSettings
			.AsNoTracking()
			.FirstOrDefaultAsync(setting => setting.Key == key);
	}

	public async Task SetAsync(string key, string? value)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Setting key cannot be null or empty.", nameof(key));
		}

		var existing = await _context.AppSettings
			.FirstOrDefaultAsync(setting => setting.Key == key);

		if (existing == null)
		{
			_context.AppSettings.Add(new AppSetting
			{
				Key = key,
				Value = value,
				UpdatedAt = DateTimeOffset.UtcNow
			});
		}
		else
		{
			existing.Value = value;
			existing.UpdatedAt = DateTimeOffset.UtcNow;
		}

		await _context.SaveChangesAsync();
	}
}

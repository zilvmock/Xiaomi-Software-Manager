using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data.Interfaces;
using xsm.Models;

namespace xsm.Data.Repositories;

public sealed class DownloadDomainRepository : IDownloadDomainRepository
{
	private readonly AppDbContext _context;

	public DownloadDomainRepository(AppDbContext context)
	{
		_context = context;
	}

	public async Task<List<DownloadDomain>> GetAllAsync()
	{
		return await _context.DownloadDomains
			.AsNoTracking()
			.OrderBy(domain => domain.Domain)
			.ToListAsync();
	}

	public async Task<DownloadDomain?> GetByDomainAsync(string domain)
	{
		if (string.IsNullOrWhiteSpace(domain))
		{
			throw new ArgumentException("Domain cannot be null or empty.", nameof(domain));
		}

		return await _context.DownloadDomains
			.FirstOrDefaultAsync(item => item.Domain == domain);
	}

	public async Task AddAsync(DownloadDomain downloadDomain)
	{
		ArgumentNullException.ThrowIfNull(downloadDomain);

		_context.DownloadDomains.Add(downloadDomain);
		await _context.SaveChangesAsync();
	}

	public async Task UpdateAsync(DownloadDomain downloadDomain)
	{
		ArgumentNullException.ThrowIfNull(downloadDomain);

		var existing = await _context.DownloadDomains
			.FirstOrDefaultAsync(item => item.Id == downloadDomain.Id)
			?? throw new KeyNotFoundException($"Download domain with ID {downloadDomain.Id} not found.");

		_context.Entry(existing).CurrentValues.SetValues(downloadDomain);
		await _context.SaveChangesAsync();
	}

	public async Task RemoveRangeAsync(IEnumerable<DownloadDomain> downloadDomains)
	{
		ArgumentNullException.ThrowIfNull(downloadDomains);

		_context.DownloadDomains.RemoveRange(downloadDomains);
		await _context.SaveChangesAsync();
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data.Interfaces;
using xsm.Models;

namespace xsm.Data.Repositories
{
	public class SoftwareRepository : ISoftwareRepository
	{
		private readonly AppDbContext _context;

		public SoftwareRepository(AppDbContext context)
		{
			_context = context;
		}

		public async Task<List<Software>> GetAllAsync()
		{
			return await _context.Software
				.Include(s => s.Regions)
				.AsNoTracking()
				.ToListAsync();
		}

		public async Task<Software?> GetByIdAsync(int id)
		{
			return await _context.Software
				.Include(s => s.Regions)
				.FirstOrDefaultAsync(s => s.Id == id);
		}

		public async Task<Software?> GetByNameAndRegionAcronymAsync(string modelName, string regionAcronym)
		{
			if (string.IsNullOrWhiteSpace(modelName))
				throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));

			if (string.IsNullOrWhiteSpace(regionAcronym))
				throw new ArgumentException("Region acronym cannot be null or empty.", nameof(regionAcronym));

			return await _context.Software
				.Include(s => s.Regions)
				.AsNoTracking()
				.FirstOrDefaultAsync(s => s.Name == modelName &&
					s.Regions.Any(r => r.Acronym == regionAcronym));
		}

		public async Task AssignRegionToSoftwareAsync(int softwareId, int regionId)
		{
			var hasTransaction = _context.Database.CurrentTransaction != null;
			await using var transaction = hasTransaction
				? null
				: await _context.Database.BeginTransactionAsync();
			try
			{
				var software = await _context.Software
				.Include(s => s.Regions)
				.FirstOrDefaultAsync(s => s.Id == softwareId)
				?? throw new KeyNotFoundException($"Software with ID {softwareId} not found.");

				var region = await _context.Regions
					.FirstOrDefaultAsync(r => r.Id == regionId)
					?? throw new KeyNotFoundException($"Region with ID {regionId} not found.");

				if (software.Regions.Any(r => r.Id == regionId))
					return; // Region already assigned, no action needed

				software.Regions.Add(region);
				await _context.SaveChangesAsync();
				if (transaction != null)
				{
					await transaction.CommitAsync();
				}
			}
			catch
			{
				if (transaction != null)
				{
					await transaction.RollbackAsync();
				}
				throw;
			}
		}

		public async Task<Software> AddAsync(Software software)
		{
			ArgumentNullException.ThrowIfNull(software);

			await _context.Software.AddAsync(software);
			await _context.SaveChangesAsync();
			return software;
		}

		public async Task UpdateAsync(Software software)
		{
			ArgumentNullException.ThrowIfNull(software);

			var existingSoftware = await _context.Software
				.Include(s => s.Regions)
				.FirstOrDefaultAsync(s => s.Id == software.Id)
				?? throw new KeyNotFoundException($"Software with ID {software.Id} not found.");

			// Detach the existing entity and attach the updated one.
			// This only updates properties that have actually changed.
			_context.Entry(existingSoftware).CurrentValues.SetValues(software);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(int id)
		{
			var software = await _context.Software
				.Include(s => s.Regions)
				.FirstOrDefaultAsync(s => s.Id == id)
				?? throw new KeyNotFoundException($"Software with ID {id} not found.");

			_context.Software.Remove(software);
			await _context.SaveChangesAsync();
		}
	}
}

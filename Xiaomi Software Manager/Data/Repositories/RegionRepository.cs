using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data.Interfaces;
using xsm.Models;

namespace xsm.Data.Repositories
{
	public class RegionRepository : IRegionRepository
	{
		private readonly AppDbContext _context;
		private readonly Dictionary<string, string> _regionRefDictionary = new()
		{
			{ "Global", "Global" },
			{ "EEA", "European Economic Area" },
			{ "IN", "India" },
			{ "TW", "Taiwan" },
			{ "ID", "Indonesia" },
			{ "RU", "Russia" },
			{ "TR", "Turkey" },
			{ "JP", "Japan" },
			{ "DC", "Digital Channel" },
		};

		/// <summary>
		/// Region "acronym, full name" dictionary of known regions to exist.
		/// </summary>
		public Dictionary<string, string> RegionRef => _regionRefDictionary;

		public RegionRepository(AppDbContext context)
		{
			_context = context;
		}

		public async Task<List<Region>> GetAllAsync()
		{
			return await _context.Regions
				.ToListAsync();
		}

		public async Task<Region?> GetRegionByNameAsync(string regionName)
		{
			if (string.IsNullOrWhiteSpace(regionName))
				throw new ArgumentException("Region name cannot be null or empty.", nameof(regionName));

			return await _context.Regions
				.FirstOrDefaultAsync(r => r.Name == regionName);
		}

		public async Task<Region?> GetByIdAsync(int id)
		{
			return await _context.Regions.FindAsync(id);
		}

		public async Task<Region> AddAsync(Region region)
		{
			ArgumentNullException.ThrowIfNull(region);

			var exists = await _context.Regions
				.AnyAsync(r => r.Name == region.Name || r.Acronym == region.Acronym);

			if (exists)
				throw new InvalidOperationException($"Region with name '{region.Name}' already exists.");

			_context.Regions.Add(region);
			await _context.SaveChangesAsync();
			return region;
		}

		public async Task UpdateAsync(Region region)
		{
			ArgumentNullException.ThrowIfNull(region);

			var existingRegion = await _context.Regions.FindAsync(region.Id) 
				?? throw new KeyNotFoundException($"Region with ID {region.Id} not found.");
			// Detach the existing entity and attach the updated one.
			// This only updates properties that have actually changed.
			_context.Entry(existingRegion).CurrentValues.SetValues(region);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(int id)
		{
			var region = await _context.Regions.FindAsync(id) 
				?? throw new KeyNotFoundException($"Region with ID {id} not found.");
			_context.Regions.Remove(region);
			await _context.SaveChangesAsync();
		}
	}
}

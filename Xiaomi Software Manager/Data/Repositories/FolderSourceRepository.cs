using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xsm.Data.Interfaces;
using xsm.Models;

namespace xsm.Data.Repositories
{
	public class FolderSourceRepository : IFolderSourceRepository
	{

		private readonly AppDbContext _context;

		public FolderSourceRepository(AppDbContext context)
		{
			_context = context;
		}

		public async Task<List<FolderSource>> GetAllAsync()
		{
			return await _context.FolderSources
				.AsNoTracking()
				.ToListAsync();
		}

		public async Task<FolderSource?> GetByIdAsync(int id)
		{
			return await _context.FolderSources
				.FirstOrDefaultAsync(fs => fs.Id == id);
		}

		public async Task<FolderSource?> GetByNameAsync(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Name cannot be null or empty.", nameof(name));

			return await _context.FolderSources
				.AsNoTracking()
				.FirstOrDefaultAsync(fs => fs.Name == name);
		}

		public async Task<FolderSource> AddAsync(FolderSource folderSource)
		{
			ArgumentNullException.ThrowIfNull(folderSource);

			var exists = await _context.FolderSources
				.AnyAsync(fs => fs.Name == folderSource.Name);

			if (exists)
				throw new InvalidOperationException($"Folder source with name '{folderSource.Name}' already exists.");

			await _context.FolderSources.AddAsync(folderSource);
			await _context.SaveChangesAsync();
			return folderSource;
		}

		public async Task UpdateAsync(FolderSource folderSource)
		{
			ArgumentNullException.ThrowIfNull(folderSource);

			var existingFolderSource = await _context.FolderSources
				.FirstOrDefaultAsync(fs => fs.Id == folderSource.Id)
				?? throw new KeyNotFoundException($"Folder source with ID {folderSource.Id} not found.");

			if (folderSource.Name != existingFolderSource.Name)
			{
				var nameExists = await _context.FolderSources
					.AnyAsync(fs => fs.Name == folderSource.Name && fs.Id != folderSource.Id);

				if (nameExists) { throw new InvalidOperationException($"Folder source with name '{folderSource.Name}' already exists."); }
			}
			if (folderSource.Path != existingFolderSource.Path)
			{
				var pathExists = await _context.FolderSources
					.AnyAsync(fs => fs.Path == folderSource.Path && fs.Id != folderSource.Id);

				if (pathExists) { throw new InvalidOperationException($"Folder source with path '{folderSource.Path}' already exists."); }
			}

			existingFolderSource.Name = folderSource.Name;
			existingFolderSource.Path = folderSource.Path;
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(int id)
		{
			var folderSource = await _context.FolderSources
				.FirstOrDefaultAsync(fs => fs.Id == id)
				?? throw new KeyNotFoundException($"Folder source with ID {id} not found.");

			_context.FolderSources.Remove(folderSource);
			await _context.SaveChangesAsync();
		}
	}
}

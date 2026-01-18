using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace xsm.Data
{
	public static class AppDbContextFactory
	{
		public static AppDbContext Create()
		{
			var dbPath = DbPath.GetDefaultPath();
			var dbDirectory = Path.GetDirectoryName(dbPath);
			if (!string.IsNullOrWhiteSpace(dbDirectory))
			{
				Directory.CreateDirectory(dbDirectory);
			}

			var builder = new SqliteConnectionStringBuilder
			{
				DataSource = dbPath,
				DefaultTimeout = 5
			};

			var options = new DbContextOptionsBuilder<AppDbContext>()
				.UseSqlite(builder.ConnectionString)
				.Options;

			return new AppDbContext(options);
		}
	}
}

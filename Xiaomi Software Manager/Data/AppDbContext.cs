using Microsoft.EntityFrameworkCore;
using xsm.Models;

namespace xsm.Data;

public sealed class AppDbContext : DbContext
{
	public DbSet<Software> Software { get; set; }
	public DbSet<Region> Regions { get; set; }
	public DbSet<FolderSource> FolderSources { get; set; }
	public DbSet<DownloadDomain> DownloadDomains { get; set; }
    
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    { }

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Software>(entity =>
        {
            entity.HasMany(s => s.Regions)
                .WithMany(r => r.Software)
                .UsingEntity(j => j.ToTable("SoftwareRegions"));

			entity.Property(s => s.Name)
				.IsRequired()
				.HasMaxLength(64);

			entity.Property(s => s.Codename)
				.HasMaxLength(64);

			entity.Property(s => s.WebLink)
				.HasMaxLength(2048);

            entity.Property(s => s.WebVersion)
                .HasMaxLength(64);

            entity.Property(s => s.LocalVersion)
                .HasMaxLength(64);

            entity.Property(s => s.IsUpToDate)
                .HasDefaultValue(false);

            entity.Property(s => s.IsDownloading)
                .HasDefaultValue(false);
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(24);
            entity.HasIndex(r => r.Name)
                .IsUnique();

            entity.Property(r => r.Acronym)
                .IsRequired()
                .HasMaxLength(12);
            entity.HasIndex(r => r.Acronym)
                .IsUnique();
        });
        
		modelBuilder.Entity<AppSetting>(entity =>
		{
			entity.HasKey(setting => setting.Key);
			entity.Property(setting => setting.Key).HasMaxLength(128);
			entity.Property(setting => setting.Value).HasMaxLength(2048);
		});

		modelBuilder.Entity<DownloadDomain>(entity =>
		{
			entity.HasIndex(domain => domain.Domain)
				.IsUnique();

			entity.Property(domain => domain.Domain)
				.IsRequired()
				.HasMaxLength(255);

			entity.Property(domain => domain.Type)
				.HasMaxLength(64);

			entity.Property(domain => domain.PrimaryRegion)
				.HasMaxLength(128);

			entity.Property(domain => domain.Infrastructure)
				.HasMaxLength(128);

			entity.Property(domain => domain.OptimizationPriority)
				.HasMaxLength(128);

			entity.Property(domain => domain.LastStatus)
				.HasMaxLength(32);

			entity.Property(domain => domain.LastError)
				.HasMaxLength(512);
		});
	}
}

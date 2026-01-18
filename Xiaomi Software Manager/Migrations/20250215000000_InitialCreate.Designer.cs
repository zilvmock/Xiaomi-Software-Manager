using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using xsm.Data;

#nullable disable

namespace xsm.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20250215000000_InitialCreate")]
public partial class InitialCreate
{
	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder
			.HasAnnotation("ProductVersion", "9.0.0");

		modelBuilder.Entity("xsm.Models.AppSetting", b =>
		{
			b.Property<string>("Key")
				.HasColumnType("TEXT")
				.HasMaxLength(128);

			b.Property<DateTimeOffset>("UpdatedAt")
				.HasColumnType("TEXT");

			b.Property<string>("Value")
				.HasColumnType("TEXT")
				.HasMaxLength(2048);

			b.HasKey("Key");

			b.ToTable("AppSettings");
		});

		modelBuilder.Entity("xsm.Models.DownloadDomain", b =>
		{
			b.Property<int>("Id")
				.ValueGeneratedOnAdd()
				.HasColumnType("INTEGER");

			b.Property<string>("Domain")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(255);

			b.Property<string>("Infrastructure")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(128);

			b.Property<double?>("LastJitterMs")
				.HasColumnType("REAL");

			b.Property<double?>("LastLatencyMs")
				.HasColumnType("REAL");

			b.Property<string>("LastError")
				.HasColumnType("TEXT")
				.HasMaxLength(512);

			b.Property<DateTimeOffset?>("LastRatedAt")
				.HasColumnType("TEXT");

			b.Property<double?>("LastScore")
				.HasColumnType("REAL");

			b.Property<string>("LastStatus")
				.HasColumnType("TEXT")
				.HasMaxLength(32);

			b.Property<double?>("LastThroughputMBps")
				.HasColumnType("REAL");

			b.Property<string>("OptimizationPriority")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(128);

			b.Property<string>("PrimaryRegion")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(128);

			b.Property<string>("Type")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(64);

			b.HasKey("Id");

			b.HasIndex("Domain")
				.IsUnique();

			b.ToTable("DownloadDomains");
		});

		modelBuilder.Entity("xsm.Models.FolderSource", b =>
		{
			b.Property<int>("Id")
				.ValueGeneratedOnAdd()
				.HasColumnType("INTEGER");

			b.Property<string>("Name")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(32);

			b.Property<string>("Path")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(2048);

			b.HasKey("Id");

			b.ToTable("FolderSources");
		});

		modelBuilder.Entity("xsm.Models.Region", b =>
		{
			b.Property<int>("Id")
				.ValueGeneratedOnAdd()
				.HasColumnType("INTEGER");

			b.Property<string>("Acronym")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(12);

			b.Property<string>("Name")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(24);

			b.HasKey("Id");

			b.HasIndex("Acronym")
				.IsUnique();

			b.HasIndex("Name")
				.IsUnique();

			b.ToTable("Regions");
		});

		modelBuilder.Entity("xsm.Models.Software", b =>
		{
			b.Property<int>("Id")
				.ValueGeneratedOnAdd()
				.HasColumnType("INTEGER");

			b.Property<string>("Codename")
				.HasColumnType("TEXT")
				.HasMaxLength(64);

			b.Property<bool>("IsDownloading")
				.HasColumnType("INTEGER")
				.HasDefaultValue(false);

			b.Property<bool>("IsUpToDate")
				.HasColumnType("INTEGER")
				.HasDefaultValue(false);

			b.Property<string>("LocalVersion")
				.HasColumnType("TEXT")
				.HasMaxLength(64);

			b.Property<string>("Name")
				.IsRequired()
				.HasColumnType("TEXT")
				.HasMaxLength(64);

			b.Property<string>("WebLink")
				.HasColumnType("TEXT")
				.HasMaxLength(2048);

			b.Property<string>("WebVersion")
				.HasColumnType("TEXT")
				.HasMaxLength(64);

			b.HasKey("Id");

			b.ToTable("Software");
		});

		modelBuilder.Entity("SoftwareRegions", b =>
		{
			b.Property<int>("RegionsId")
				.HasColumnType("INTEGER");

			b.Property<int>("SoftwareId")
				.HasColumnType("INTEGER");

			b.HasKey("RegionsId", "SoftwareId");

			b.HasIndex("SoftwareId");

			b.ToTable("SoftwareRegions");
		});

		modelBuilder.Entity("SoftwareRegions", b =>
		{
			b.HasOne("xsm.Models.Region", null)
				.WithMany()
				.HasForeignKey("RegionsId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();

			b.HasOne("xsm.Models.Software", null)
				.WithMany()
				.HasForeignKey("SoftwareId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});

		modelBuilder.Entity("xsm.Models.Software", b =>
		{
			b.HasMany("xsm.Models.Region", "Regions")
				.WithMany()
				.UsingEntity(
					"SoftwareRegions",
					r => r.HasOne("xsm.Models.Region", null)
						.WithMany()
						.HasForeignKey("RegionsId")
						.OnDelete(DeleteBehavior.Cascade)
						.IsRequired(),
					l => l.HasOne("xsm.Models.Software", null)
						.WithMany()
						.HasForeignKey("SoftwareId")
						.OnDelete(DeleteBehavior.Cascade)
						.IsRequired(),
					j =>
					{
						j.HasKey("RegionsId", "SoftwareId");
						j.HasIndex("SoftwareId");
						j.ToTable("SoftwareRegions");
					});
		});
	}
}

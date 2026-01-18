using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xsm.Migrations;

public partial class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS Regions (
	Id INTEGER NOT NULL CONSTRAINT PK_Regions PRIMARY KEY AUTOINCREMENT,
	Name TEXT NOT NULL,
	Acronym TEXT NOT NULL
);
""");

		migrationBuilder.Sql("""
CREATE UNIQUE INDEX IF NOT EXISTS IX_Regions_Name ON Regions (Name);
""");

		migrationBuilder.Sql("""
CREATE UNIQUE INDEX IF NOT EXISTS IX_Regions_Acronym ON Regions (Acronym);
""");

		migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS Software (
	Id INTEGER NOT NULL CONSTRAINT PK_Software PRIMARY KEY AUTOINCREMENT,
	Name TEXT NOT NULL,
	Codename TEXT NULL,
	WebLink TEXT NULL,
	WebVersion TEXT NULL,
	LocalVersion TEXT NULL,
	IsUpToDate INTEGER NOT NULL DEFAULT 0,
	IsDownloading INTEGER NOT NULL DEFAULT 0
);
""");

		migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS FolderSources (
	Id INTEGER NOT NULL CONSTRAINT PK_FolderSources PRIMARY KEY AUTOINCREMENT,
	Name TEXT NOT NULL,
	Path TEXT NOT NULL
);
""");

		migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS AppSettings (
	Key TEXT NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
	Value TEXT NULL,
	UpdatedAt TEXT NOT NULL
);
""");

		migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS SoftwareRegions (
	RegionsId INTEGER NOT NULL,
	SoftwareId INTEGER NOT NULL,
	CONSTRAINT PK_SoftwareRegions PRIMARY KEY (RegionsId, SoftwareId),
	CONSTRAINT FK_SoftwareRegions_Regions_RegionsId FOREIGN KEY (RegionsId) REFERENCES Regions (Id) ON DELETE CASCADE,
	CONSTRAINT FK_SoftwareRegions_Software_SoftwareId FOREIGN KEY (SoftwareId) REFERENCES Software (Id) ON DELETE CASCADE
);
""");

		migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS IX_SoftwareRegions_SoftwareId ON SoftwareRegions (SoftwareId);
""");

		migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS DownloadDomains (
	Id INTEGER NOT NULL CONSTRAINT PK_DownloadDomains PRIMARY KEY AUTOINCREMENT,
	Domain TEXT NOT NULL,
	Type TEXT NOT NULL,
	PrimaryRegion TEXT NOT NULL,
	Infrastructure TEXT NOT NULL,
	OptimizationPriority TEXT NOT NULL,
	LastRatedAt TEXT NULL,
	LastThroughputMBps REAL NULL,
	LastLatencyMs REAL NULL,
	LastJitterMs REAL NULL,
	LastScore REAL NULL,
	LastStatus TEXT NULL,
	LastError TEXT NULL
);
""");

		migrationBuilder.Sql("""
CREATE UNIQUE INDEX IF NOT EXISTS IX_DownloadDomains_Domain ON DownloadDomains (Domain);
""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
DROP TABLE IF EXISTS SoftwareRegions;
DROP TABLE IF EXISTS DownloadDomains;
DROP TABLE IF EXISTS AppSettings;
DROP TABLE IF EXISTS FolderSources;
DROP TABLE IF EXISTS Regions;
DROP TABLE IF EXISTS Software;
""");
	}
}

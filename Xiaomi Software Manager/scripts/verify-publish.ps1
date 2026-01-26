param(
	[string]$PublishDir
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $repoRoot

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
	$PublishDir = Join-Path $repoRoot "publish/app"
}

$PublishDir = (Resolve-Path $PublishDir).Path

if (-not (Test-Path $PublishDir)) {
	throw "Publish directory not found: $PublishDir"
}

$mustExist = @(
	"xsm.exe",
	"xsm.deps.json",
	"xsm.runtimeconfig.json",
	"xsm.updater.exe"
)

foreach ($file in $mustExist) {
	$path = Join-Path $PublishDir $file
	if (-not (Test-Path $path)) {
		throw "Missing required file: $file"
	}
}

$seleniumManagerPath = Join-Path $PublishDir "selenium-manager"
if (Test-Path $seleniumManagerPath) {
	throw "selenium-manager folder should not be published."
}

$seedPath = Join-Path $PublishDir "Data/Seeds/download-domains.json"
if (Test-Path $seedPath) {
	throw "Seed JSON should not be published."
}

$extraDlls = Get-ChildItem -Path $PublishDir -Recurse -Filter *.dll
if ($extraDlls.Count -gt 0) {
	$names = $extraDlls | ForEach-Object { $_.FullName }
	throw "Unexpected DLLs found in publish output:`n$($names -join "`n")"
}

Write-Host "Publish output validation passed: $PublishDir"

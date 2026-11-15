# Builds Aether.API, the loader and the sample mods (Release).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 [-GameDir <dir>]
#
# Output: build\Aether.API\Release, build\AetherLoader\Release, build\mods\<sample>\ (each a mod folder).
param(
	[string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\CarX Drift Racing Online'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) { Write-Host "BUILD FAILED: BepInEx is not installed in $GameDir (the loader references its BepInEx.dll)"; exit 1 }
$projects = @('src\Aether.API\Aether.API.csproj', 'src\AetherLoader\AetherLoader.csproj') + (Get-ChildItem (Join-Path $repo 'samples') -Recurse -Filter *.csproj | ForEach-Object { $_.FullName.Substring($repo.Length + 1) })
foreach ($p in $projects) {
	& dotnet build (Join-Path $repo $p) -c Release -nologo -v q "-p:GameDir=$GameDir"
	if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED: $p"; exit 1 }
}
Write-Host "BUILD OK ($($projects.Count) projects)"
exit 0

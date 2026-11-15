# Installs Aether Loader into the game (game closed), or removes it.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy.ps1 [-GameDir <dir>] [-Build] [-Tests | -RemoveTests] [-Remove]
#
#   BepInEx\plugins\AetherLoader\AetherLoader.dll, Aether.API.dll   the plugin and the SDK the mods use
#   AetherLoader\mods\                                               the mods (created empty)
# -Tests also installs the sample mods and the test fixtures as mods\aether-test-<name> and switches off
#   nec093.test.switched-off in loader.json (scripts\test-game.ps1 uses this). -RemoveTests takes them out again.
# -Remove deletes the plugin; AetherLoader\ (mods, settings, logs) is kept.
param(
	[string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\CarX Drift Racing Online',
	[switch]$Build,
	[switch]$Tests,
	[switch]$RemoveTests,
	[switch]$Remove
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (Get-Process | Where-Object { $_.ProcessName -like 'Drift Racing Online*' }) { Write-Host 'DEPLOY FAILED: the game is running (the DLLs are locked). Close it first.'; exit 1 }
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) { Write-Host "DEPLOY FAILED: BepInEx is not installed in $GameDir"; exit 1 }

$plugin = Join-Path $GameDir 'BepInEx\plugins\AetherLoader'
$data = Join-Path $GameDir 'AetherLoader'
$mods = Join-Path $data 'mods'
$loaderJson = Join-Path $data 'config\loader.json'
$testOff = 'nec093.test.switched-off'

function Set-TestSwitch([bool]$off) {
	$json = if (Test-Path $loaderJson) { Get-Content $loaderJson -Raw | ConvertFrom-Json } else { [pscustomobject]@{ hotkey = 'F9'; disabled = @() } }
	$list = @($json.disabled | Where-Object { $_ -and $_ -ne $testOff })
	if ($off) { $list += $testOff }
	$json.disabled = $list
	New-Item -ItemType Directory -Force (Split-Path $loaderJson) | Out-Null
	[IO.File]::WriteAllText($loaderJson, ($json | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))
}

function Remove-TestMods {
	if (Test-Path $mods) { Get-ChildItem $mods -Directory -Filter 'aether-test-*' | ForEach-Object { Remove-Item -Recurse -Force $_.FullName } }
	if (Test-Path $loaderJson) { Set-TestSwitch $false }
}

if ($Remove) {
	if (Test-Path $plugin) { Remove-Item -Recurse -Force $plugin; Write-Host "removed $plugin" }
	Write-Host "Aether Loader removed ($data kept: mods, settings, logs)"
	exit 0
}
if ($RemoveTests) {
	Remove-TestMods
	Write-Host 'test mods removed'
	exit 0
}

if ($Build) {
	& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir
	if ($LASTEXITCODE -ne 0) { exit 1 }
}
$out = Join-Path $repo 'build\AetherLoader\Release'
foreach ($f in 'AetherLoader.dll', 'Aether.API.dll') { if (-not (Test-Path (Join-Path $out $f))) { Write-Host "DEPLOY FAILED: $out\$f not found (run with -Build)"; exit 1 } }
New-Item -ItemType Directory -Force $plugin, $mods | Out-Null
foreach ($f in 'AetherLoader.dll', 'AetherLoader.pdb', 'Aether.API.dll', 'Aether.API.pdb') {
	if (Test-Path (Join-Path $out $f)) { Copy-Item -Force (Join-Path $out $f) $plugin }
}
Write-Host "plugin: $plugin"

if ($Tests) {
	Remove-TestMods
	$sources = @(Get-ChildItem (Join-Path $repo 'build\mods') -Directory) + @(Get-ChildItem (Join-Path $repo 'tests\fixtures') -Directory)
	foreach ($s in $sources) {
		$to = Join-Path $mods ('aether-test-' + $s.Name)
		New-Item -ItemType Directory -Force $to | Out-Null
		# a mod's folder holds its own files only: Aether.API comes from the loader
		Get-ChildItem $s.FullName -File | Where-Object { $_.Name -notlike 'Aether.API.*' } | Copy-Item -Destination $to
	}
	Set-TestSwitch $true
	Write-Host "test mods: $($sources.Count) in $mods (aether-test-*)"
}
Write-Host "mods folder: $mods"
Write-Host 'DEPLOY OK'
exit 0

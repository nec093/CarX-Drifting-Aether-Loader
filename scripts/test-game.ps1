# The in-game test: builds, installs the sample mods and fixtures, starts the game, compares what the loader did
# (AetherLoader\logs\status.json, BepInEx\LogOutput.log) with tests\expected.json, closes the game and takes the
# test mods out again.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test-game.ps1 [-GameDir <dir>] [-TimeoutSeconds 240] [-Keep]
#
# -Keep leaves the game running with the test mods installed (to look at the window, F9); remove them later with
#   deploy.ps1 -RemoveTests (game closed). Exit code 0 = every check passed.
param(
	[string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\CarX Drift Racing Online',
	[int]$TimeoutSeconds = 240,
	[switch]$Keep,
	# opens the loader's window at the start (config\dev-open-window) to look at it, e.g. with scripts\screenshot.ps1
	[switch]$OpenWindow
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
function Get-Game { Get-Process | Where-Object { $_.ProcessName -like 'Drift Racing Online*' } | Select-Object -First 1 }
if (Get-Game) { Write-Host 'TEST FAILED: the game is running. Close it first.'; exit 1 }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'deploy.ps1') -GameDir $GameDir -Build -Tests
if ($LASTEXITCODE -ne 0) { exit 1 }

$devOpen = Join-Path $GameDir 'AetherLoader\config\dev-open-window'
if ($OpenWindow) { New-Item -ItemType File -Force $devOpen | Out-Null } elseif (Test-Path $devOpen) { Remove-Item -Force $devOpen }
$status = Join-Path $GameDir 'AetherLoader\logs\status.json'
$log = Join-Path $GameDir 'BepInEx\LogOutput.log'
$started = Get-Date
$steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamExe
if (-not $steam -or -not (Test-Path $steam)) { $steam = "${env:ProgramFiles(x86)}\Steam\steam.exe" }
Start-Process -FilePath $steam -ArgumentList @('-applaunch', 635260, '-force-d3d12')
$deadline = $started.AddSeconds($TimeoutSeconds)
while (-not ((Test-Path $status) -and (Get-Item $status).LastWriteTime -gt $started)) {
	if ((Get-Date) -gt $deadline) { Write-Host "TEST FAILED: no new status.json within $TimeoutSeconds s (is the loader in BepInEx\plugins? see $log)"; exit 1 }
	Start-Sleep -Milliseconds 500
}
# the sample's Update line comes a few frames later
$updateDeadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $updateDeadline -and -not (Select-String -Path $log -SimpleMatch 'Update runs' -Quiet)) { Start-Sleep -Milliseconds 500 }

$expected = Get-Content (Join-Path $repo 'tests\expected.json') -Raw | ConvertFrom-Json
$mods = (Get-Content $status -Raw | ConvertFrom-Json).mods
$failures = 0
function Check([string]$name, [bool]$ok, [string]$detail) {
	if ($ok) { Write-Host "  ok    $name" } else { Write-Host "  FAIL  $name $detail"; $script:failures++ }
}
foreach ($p in $expected.states.PSObject.Properties) {
	$m = $mods | Where-Object { $_.folder -eq ('aether-test-' + $p.Name) }
	Check "$($p.Name): $($p.Value)" ($m -and $m.state -eq $p.Value) $(if ($m) { "(was $($m.state): $($m.reason))" } else { '(not in status.json)' })
	if ($m -and $m.state -ne 'Loaded') { Write-Host "          reason: $($m.reason)" }
}
foreach ($pair in $expected.loadsBefore) {
	$a = $mods | Where-Object { $_.folder -eq ('aether-test-' + $pair[0]) }
	$b = $mods | Where-Object { $_.folder -eq ('aether-test-' + $pair[1]) }
	Check "$($pair[0]) loads before $($pair[1])" ($a -and $b -and $a.loadIndex -ge 0 -and $a.loadIndex -lt $b.loadIndex) "($($a.loadIndex), $($b.loadIndex))"
}
foreach ($line in $expected.logLines) {
	Check "log: $line" (Select-String -Path $log -SimpleMatch $line -Quiet) ''
}
Check 'not-a-mod is ignored' (-not ($mods | Where-Object { $_.folder -eq 'aether-test-not-a-mod' })) ''

if (-not $Keep) {
	$game = Get-Game
	if ($game) { $null = $game.CloseMainWindow(); $null = $game.WaitForExit(30000) }
	if (Get-Game) { Write-Host 'note: the game did not close; the test mods stay installed (deploy.ps1 -RemoveTests after closing it)' }
	else { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'deploy.ps1') -GameDir $GameDir -RemoveTests | Out-Null }
}
if ($failures -eq 0) { Write-Host 'GAME TEST OK'; exit 0 }
Write-Host "GAME TEST FAILED ($failures)"
exit 1

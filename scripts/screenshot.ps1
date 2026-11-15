# Saves what the screen shows inside the game's window as a PNG (to look at the loader's window without touching the game:
# no keys, no clicks, no focus change). The game must be visible on screen.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\screenshot.ps1 [-Out build\screenshots\window.png]
param(
	[string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'build\screenshots\window.png')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class AetherWin {
	[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
	[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
	[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
$null = [AetherWin]::SetProcessDPIAware()
$game = Get-Process | Where-Object { $_.ProcessName -like 'Drift Racing Online*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $game) { Write-Host 'the game is not running'; exit 1 }
$r = New-Object AetherWin+RECT
$null = [AetherWin]::GetWindowRect($game.MainWindowHandle, [ref]$r)
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "saved $Out ($w x $h)"

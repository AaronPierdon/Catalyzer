# Debug build + render every theme (and settings, the close prompt, the feed, the notes) to _devSpace\snap\*.png, then print the log tail.
# Usage (from a linked chat):  bash .catalyzer/link/run.sh -t 600 -f tools/snap.ps1
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent
$out = dotnet build "$root\Catalyzer.slnx" -c Debug --nologo 2>&1
$out | Where-Object { $_ -match ' error | warning CS' -and $_ -notmatch 'Nexus.Kit' } | Select-Object -Unique | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE) { Write-Output 'BUILD FAILED'; exit 1 }
Write-Output 'BUILD OK'
$bin = "$root\src\Catalyzer\bin\Debug\net10.0-windows"
# the Nexus plugin isn't copied by its own build (it carries Nexus.Kit): bring it into the Debug app's plugins folder
$nx = "$root\plugins\Catalyzer.Nexus\bin\Debug\net10.0-windows"
if (Test-Path $nx) {
    New-Item "$bin\plugins\Nexus" -ItemType Directory -Force | Out-Null
    Copy-Item "$nx\*" "$bin\plugins\Nexus" -Recurse -Force
    Remove-Item "$bin\plugins\Nexus\Catalyzer.Sdk.*" -ErrorAction SilentlyContinue
}
$dir = "$root\_devSpace\snap"; New-Item $dir -ItemType Directory -Force | Out-Null
Remove-Item "$dir\*.png" -ErrorAction SilentlyContinue
$p = Start-Process "$bin\Catalyzer.exe" -ArgumentList '--snapshot', "`"$dir`"" -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit(120000)) { $p.Kill(); Write-Output 'snapshot timed out' }
Get-ChildItem $dir -Filter *.png | ForEach-Object { Write-Output ("snap " + $_.Name + " " + $_.Length) }
Get-Content "$env:LOCALAPPDATA\Catalyzer\log.txt" -Tail 12 -ErrorAction SilentlyContinue

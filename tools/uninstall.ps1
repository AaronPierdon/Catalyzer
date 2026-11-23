# Uninstalls Catalyzer (Settings > Apps runs this). Removes the program, its shortcut, the run-at-start entry and the
# Apps entry. Keeps your settings, notes links and plugin data in %APPDATA%\Catalyzer unless you pass -Purge.
# Your projects are never touched.
param([switch]$Purge)
$ErrorActionPreference = 'Continue'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Catalyzer'
# run from a temp copy: this script lives in the folder it deletes
if ($PSCommandPath -like "$dest*") {
    $tmp = Join-Path $env:TEMP 'catalyzer-uninstall.ps1'
    Copy-Item $PSCommandPath $tmp -Force
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$tmp`""); if ($Purge) { $a += '-Purge' }
    Start-Process powershell.exe -ArgumentList $a -WindowStyle Hidden
    exit 0
}
Get-Process Catalyzer -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
Start-Sleep -Milliseconds 1500
Get-Process Catalyzer -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process Catalyzer -ErrorAction SilentlyContinue | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Catalyzer.lnk') -ErrorAction SilentlyContinue
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Catalyzer -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Catalyzer' -Recurse -ErrorAction SilentlyContinue
if ($Purge) { Remove-Item (Join-Path $env:APPDATA 'Catalyzer') -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item (Join-Path $env:LOCALAPPDATA 'Catalyzer') -Recurse -Force -ErrorAction SilentlyContinue }

# Installs <App> on this PC (Release) to %LOCALAPPDATA%\Programs\<App>, on top of what's there, and starts it.
# The install contract (principles 07): build FIRST (a failed build leaves the running app alone), then close the
# app, copy, restart it; -Log writes a transcript; exit 0 = installed, 1 = failed (the old copy is started again if
# it was closed). Catalyzer's UPDATE chip runs this hidden; a linked chat runs:
#   bash .catalyzer/link/run.sh -f tools/install.ps1      (warn Aaron first if <App> is running)
param([switch]$NoStart, [string]$Log)
$ErrorActionPreference = 'Stop'
if ($Log) { try { Start-Transcript -Path $Log -Force | Out-Null } catch { } }
$app   = '<App>'
$root  = Split-Path $PSScriptRoot -Parent
$dest  = Join-Path $env:LOCALAPPDATA "Programs\$app"
$stage = Join-Path $root '_devSpace\publish'
$closed = $false
try {
    # 1. build into _devSpace (the app keeps running meanwhile)
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $stage) { $stage = Join-Path $root ('_devSpace\publish-' + (Get-Date -Format 'HHmmss')) }
    $proj = Get-ChildItem "$root\src" -Recurse -Filter "$app.csproj" | Select-Object -First 1
    if (-not $proj) { throw "src\...\$app.csproj not found" }
    dotnet publish $proj.FullName -c Release -o $stage --nologo -v q
    if ($LASTEXITCODE) { throw "build failed" }

    # 2. close the running copy
    Get-Process $app -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Milliseconds 800
    Get-Process $app -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process $app -ErrorAction SilentlyContinue | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    $closed = $true

    # 3. install on top (settings live in %APPDATA%\<App>, untouched) + a Start menu shortcut
    New-Item $dest -ItemType Directory -Force | Out-Null
    Copy-Item "$stage\*" $dest -Recurse -Force
    $lnk = Join-Path ([Environment]::GetFolderPath('Programs')) "$app.lnk"
    $sh = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk); $sh.TargetPath = Join-Path $dest "$app.exe"; $sh.WorkingDirectory = $dest; $sh.Save()
    $ver = (Get-Item (Join-Path $dest "$app.exe")).VersionInfo.ProductVersion -replace '\+.*', ''
    "$app $ver installed to $dest"
    if (-not $NoStart) { Start-Process (Join-Path $dest "$app.exe") }
}
catch {
    "INSTALL FAILED: $_"
    if ($closed -and -not $NoStart -and (Test-Path (Join-Path $dest "$app.exe")) -and -not (Get-Process $app -ErrorAction SilentlyContinue)) { Start-Process (Join-Path $dest "$app.exe") }
    if ($Log) { try { Stop-Transcript | Out-Null } catch { } }
    exit 1
}
if ($Log) { try { Stop-Transcript | Out-Null } catch { } }

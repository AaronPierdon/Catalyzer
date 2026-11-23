# Release build of <App> into %LOCALAPPDATA%\Programs\<App>, a Start menu shortcut, then start it.
# A linked chat runs:  bash .catalyzer/link/run.sh -f tools/install.ps1   (warn Aaron first if <App> is running)
$ErrorActionPreference = 'Stop'
$app  = '<App>'
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $env:LOCALAPPDATA "Programs\$app"
Get-Process $app -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null; if (-not $_.WaitForExit(3000)) { $_.Kill() } }
dotnet publish (Get-ChildItem "$root\src" -Recurse -Filter "$app.csproj" | Select-Object -First 1).FullName -c Release -o $dest --nologo
if ($LASTEXITCODE) { 'PUBLISH FAILED'; exit 1 }
$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) "$app.lnk"
$sh = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk); $sh.TargetPath = Join-Path $dest "$app.exe"; $sh.Save()
Start-Process (Join-Path $dest "$app.exe")
"$app installed to $dest"

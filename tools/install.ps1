# Builds Catalyzer (Release) and installs it to %LOCALAPPDATA%\Programs\Catalyzer with its bundled plugins.
# Usage: tools\install.ps1 [-NoStart]
param([switch]$NoStart)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Catalyzer'
$stage = Join-Path $root 'publish'

Get-Process Catalyzer -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
Start-Sleep -Milliseconds 800
Get-Process Catalyzer -ErrorAction SilentlyContinue | Stop-Process -Force
# wait until it has really gone: a killed process lets go of its files a moment later
Get-Process Catalyzer -ErrorAction SilentlyContinue | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue

# the staging folder can stay locked for a moment (the old process, antivirus): retry, then use a fresh one
for ($i = 0; $i -lt 10 -and (Test-Path $stage); $i++) {
    try { Remove-Item $stage -Recurse -Force -ErrorAction Stop } catch { Start-Sleep -Milliseconds 500 }
}
if (Test-Path $stage) { $stage = Join-Path $root ('publish-' + (Get-Date -Format 'HHmmss')); Write-Host "publish was locked: staging in $stage" }
dotnet publish "$root\src\Catalyzer\Catalyzer.csproj" -c Release -o "$stage\app" --nologo -v q
if ($LASTEXITCODE) { throw "Catalyzer build failed" }
# every plugin under plugins\* -> plugins\<Name without the Catalyzer. prefix>
foreach ($proj in Get-ChildItem "$root\plugins" -Recurse -Filter *.csproj) {
    $name = $proj.BaseName -replace '^Catalyzer\.', ''
    dotnet publish $proj.FullName -c Release -o "$stage\app\plugins\$name" --nologo -v q
    if ($LASTEXITCODE) { Write-Warning "plugin $name failed to build - skipped"; Remove-Item "$stage\app\plugins\$name" -Recurse -Force -ErrorAction SilentlyContinue }
    else { Remove-Item "$stage\app\plugins\$name\Catalyzer.Sdk.*" -ErrorAction SilentlyContinue }   # the host's Sdk is the only Sdk
}

New-Item $dest -ItemType Directory -Force | Out-Null
if (Test-Path "$dest\plugins") { Remove-Item "$dest\plugins" -Recurse -Force }   # bundled plugins are replaced whole (yours live in %APPDATA%)
Copy-Item "$stage\app\*" $dest -Recurse -Force
# the principles travel with the app: NEW PROJECT builds new repos from principles\templates
if (Test-Path "$dest\principles") { Remove-Item "$dest\principles" -Recurse -Force }
Copy-Item "$root\principles" "$dest\principles" -Recurse -Force
Copy-Item "$root\tools\uninstall.ps1" "$dest\uninstall.ps1" -Force

$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Catalyzer.lnk'
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk); $s.TargetPath = "$dest\Catalyzer.exe"; $s.WorkingDirectory = $dest; $s.IconLocation = "$dest\Catalyzer.exe,0"; $s.Description = 'Project command center'; $s.Save()

# keep the Run key pointing at this copy if you turned "Run at Windows start" on
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty $run -Name Catalyzer -ErrorAction SilentlyContinue) { Set-ItemProperty $run -Name Catalyzer -Value "`"$dest\Catalyzer.exe`" --autostart" }

# Settings → Apps → Installed apps (per user, no admin): name, version, publisher, icon, Uninstall
$ver = (Get-Item "$dest\Catalyzer.exe").VersionInfo.ProductVersion -replace '\+.*', ''
$un = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Catalyzer'
New-Item $un -Force | Out-Null
$size = [int]((Get-ChildItem $dest -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
@{ DisplayName = 'Catalyzer'; DisplayVersion = "$ver"; Publisher = 'Aaron Pierdon'; InstallLocation = $dest; DisplayIcon = "$dest\Catalyzer.exe,0"
   UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$dest\uninstall.ps1`""; NoModify = 1; NoRepair = 1; EstimatedSize = $size }.GetEnumerator() |
   ForEach-Object { Set-ItemProperty $un -Name $_.Key -Value $_.Value }

Write-Host "Catalyzer $ver installed to $dest"
if (-not $NoStart) { Start-Process "$dest\Catalyzer.exe" }

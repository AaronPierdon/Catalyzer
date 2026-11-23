# Build <App> (Debug). A linked chat runs:  bash .catalyzer/link/run.sh -f tools/build.ps1
# Aaron can double-click tools\build.cmd. Prints errors and a one-line result; exit code 0 = built.
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent
$out = dotnet build (Get-ChildItem $root -Filter *.sln* | Select-Object -First 1).FullName -c Debug --nologo -m:2 2>&1
$out | Where-Object { $_ -match ' error | warning CS' } | Select-Object -Unique
if ($LASTEXITCODE) { 'BUILD FAILED'; exit 1 } else { 'BUILD OK' }

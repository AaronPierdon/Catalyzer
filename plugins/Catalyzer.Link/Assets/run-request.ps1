# Catalyzer link: runs one request hidden and writes its output itself (so a request that outlives Catalyzer still logs).
param([string]$Script, [string]$Log, [string]$Done, [string]$Root)
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }
$OutputEncoding = [Text.Encoding]::UTF8
$env:GIT_TERMINAL_PROMPT = '0'
$fs = [IO.File]::Open($Log, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
$w = New-Object IO.StreamWriter($fs, (New-Object Text.UTF8Encoding($false)))
$w.AutoFlush = $true
function Out-Line($o) {
    if ($o -is [System.Management.Automation.ErrorRecord]) { $w.WriteLine('! ' + $o.ToString()) }
    elseif ($o -is [string]) { $w.WriteLine($o) }
    else { foreach ($l in ($o | Out-String -Stream -Width 220)) { if ($l -ne '') { $w.WriteLine($l) } } }
}
$code = 0
try {
    Set-Location -LiteralPath $Root
    $global:LASTEXITCODE = 0
    & $Script 2>&1 | ForEach-Object { Out-Line $_ }
    $code = $global:LASTEXITCODE
    if ($null -eq $code) { $code = 0 }
}
catch { $w.WriteLine('! ' + $_.ToString()); $code = 1 }
finally { $w.Close() }
[IO.File]::WriteAllText($Done, [string]$code)
exit $code

# Read-only launch diagnostics; does not change Windows security settings.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$path = Join-Path $PSScriptRoot 'Setup.exe'
if (-not (Test-Path -LiteralPath $path)) { throw 'Setup.exe was not found beside this script.' }
$startedAt = Get-Date
Write-Host ("Launching: " + $path)
try {
    $signature = Get-AuthenticodeSignature -FilePath $path
    Write-Host ("Signature status: " + $signature.Status + ' (this development build is unsigned).')
    $process = Start-Process -FilePath $path -PassThru
    Write-Host ("Windows created process ID: " + $process.Id)
    if ($process.WaitForExit(3000)) { Write-Host ("Setup exited. Exit code: " + $process.ExitCode) }
    else { Write-Host 'Setup is still running. Its window should now be visible.' }
} catch { Write-Host ("Windows launch error: " + $_.Exception.Message) -ForegroundColor Red }
$logs = @(
    (Join-Path $env:LOCALAPPDATA 'CyborgVitals\Logs\setup-startup.log'),
    (Join-Path ([IO.Path]::GetTempPath()) 'CyborgVitals-setup-startup.log')
)
$found = $false
foreach ($log in $logs) {
    if (Test-Path -LiteralPath $log) {
        $file = Get-Item -LiteralPath $log
        if ($file.LastWriteTime -ge $startedAt.AddSeconds(-2)) {
            $found = $true
            Write-Host ("Startup log: " + $log)
            Get-Content -LiteralPath $log -Tail 50
        }
    }
}
if (-not $found) { Write-Host 'No fresh managed startup log was found. The cause may be before the application entry point; Windows events may provide details.' }
try {
    $events = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $startedAt.AddMinutes(-2) } -ErrorAction Stop |
        Where-Object { $_.ProviderName -in '.NET Runtime', 'Application Error', 'SideBySide', 'Windows Error Reporting' -and
            ($_.Message -match 'CyborgVitals' -or $_.Message -match '(?i)\bSetup\.exe\b') } | Select-Object -First 4)
    foreach ($event in $events) { Write-Host ("Windows event " + $event.Id + ' / ' + $event.ProviderName); Write-Host $event.Message }
} catch { Write-Host 'No matching application events were readable in this time window.' }
Write-Host 'Share this output if the window still does not appear.'

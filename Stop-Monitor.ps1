# Request a graceful shutdown from this account's reader. No elevation or forced termination.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$running = @(Get-Process -Name 'CyborgVitals.Collector' -ErrorAction SilentlyContinue)
if (-not $running.Count) { Write-Host 'Sensor reader is already stopped.'; exit 0 }
$package = Get-AppxPackage -Name 'CyborgVitals' | Select-Object -First 1
if (-not $package) { throw 'The widget package is not installed for this account. End only CyborgVitals.Collector.exe in Task Manager if an old reader is still running.' }
$folder = Join-Path $env:LOCALAPPDATA ('Packages\' + $package.PackageFamilyName + '\LocalState')
$path = Join-Path $folder 'telemetry.json'
if (-not (Test-Path -LiteralPath $path)) { throw 'No reader status file exists yet. Wait a few seconds and try again.' }
$status = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
if (-not $status.PSObject.Properties['readerProcessId']) {
    throw 'This is an older reader. Exit it once using its tray icon before installing the new background-only version.'
}
$readerId = [int]$status.readerProcessId
$reader = Get-Process -Id $readerId -ErrorAction SilentlyContinue
if (-not $reader -or $reader.ProcessName -ne 'CyborgVitals.Collector') { Write-Host 'This widget reader is already stopped.'; exit 0 }
$request = @{ processId = $readerId; timestampUnixMs = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() } | ConvertTo-Json -Compress
$temp = Join-Path $folder 'reader-stop.tmp'
[IO.File]::WriteAllText($temp, $request, (New-Object Text.UTF8Encoding($false)))
Move-Item -LiteralPath $temp -Destination (Join-Path $folder 'reader-stop.json') -Force
$clock = [Diagnostics.Stopwatch]::StartNew()
while ($clock.Elapsed.TotalSeconds -lt 10) {
    if (-not (Get-Process -Id $readerId -ErrorAction SilentlyContinue)) { Write-Host 'Sensor reader stopped.'; exit 0 }
    Start-Sleep -Milliseconds 250
}
throw 'Stop requested; reader is still finishing a hardware call. Wait and retry. A stuck reader can be ended by selecting only CyborgVitals.Collector.exe in Task Manager.'

[CmdletBinding()]
param([switch]$EnableHardwareSensors, [switch]$EnableMotherboardFanProbing, [switch]$ShowTrayIcon)
$ErrorActionPreference = 'Stop'
if ($EnableMotherboardFanProbing) { $EnableHardwareSensors = $true }
if (-not $EnableHardwareSensors) {
    Write-Host 'CPU/RAM monitoring works inside the widget without a background reader.'
    Write-Host 'For temperatures: run this script with -EnableHardwareSensors.'
    Write-Host 'For optional motherboard fan probing: add -EnableMotherboardFanProbing after checking temperatures.'
    exit 0
}

try { [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false) } catch { }
$package = Get-AppxPackage -Name 'CyborgVitals' | Select-Object -First 1
if (-not $package) { throw 'Install the widget first with Build-And-Install.ps1. See START-HERE.md.' }
if (Get-Process -Name 'CyborgVitals.Collector' -ErrorAction SilentlyContinue) {
    throw 'The reader is already running. Run Stop-Monitor.ps1 (or use Stop reader in Setup) before changing options. For an older build, exit its tray icon once.'
}
$installedRoot = Join-Path $env:LOCALAPPDATA 'CyborgVitals'
$executable = Join-Path $installedRoot 'dist\Collector\CyborgVitals.Collector.exe'
if (-not (Test-Path -LiteralPath $executable)) { $executable = Join-Path $PSScriptRoot 'dist\Collector\CyborgVitals.Collector.exe' }
if (-not (Test-Path $executable)) { throw 'The sensor reader is missing. Run Build-And-Install.ps1 first.' }
$output = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalState"
New-Item -ItemType Directory -Path $output -Force | Out-Null
# RunAs causes a visible UAC prompt; no scheduled task, service, or automatic startup is added.
$readerArgs = @('--output', ('"' + $output + '"'), '--hardware-sensors')
if ($EnableMotherboardFanProbing) { $readerArgs += '--motherboard-fans' }
if ($ShowTrayIcon) { $readerArgs += '--show-tray' }
$reader = Start-Process -FilePath $executable -ArgumentList $readerArgs -Verb RunAs -PassThru
$connected = $null
$watch = [Diagnostics.Stopwatch]::StartNew()
while ($watch.Elapsed.TotalSeconds -lt 6) {
    if (-not (Get-Process -Id $reader.Id -ErrorAction SilentlyContinue)) {
        throw 'The reader exited immediately. Run Diagnose-Readings.ps1 and share its output.'
    }
    try {
        $status = Get-Content -LiteralPath (Join-Path $output 'telemetry.json') -Raw -ErrorAction Stop | ConvertFrom-Json
        if ($status.PSObject.Properties['readerProcessId'] -and [int]$status.readerProcessId -eq $reader.Id -and
            [math]::Abs([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() - $status.timestampUnixMs) -lt 5000) {
            $connected = $status; break
        }
    } catch { }
    Start-Sleep -Milliseconds 250
}
if ($null -ne $connected) {
    Write-Host ('Reader connected, PID ' + $reader.Id + '.') -ForegroundColor Green
    Write-Host ('Sensor driver: ' + $connected.sensorDriverStatus)
    Write-Host ('Hardware status: ' + $connected.hardwareMonitoringStatus)
} else { Write-Warning 'Reader launched, but no fresh heartbeat was received yet. Run Diagnose-Readings.ps1.' }
Write-Host 'Press Win+G > Widget menu > Cyborg Vitals, then click its pin.'
Write-Host 'The reader runs in the background without a notification-area icon by default. Use Stop-Monitor.ps1 or Stop reader in Setup to stop it.'
Write-Host 'Sensor access requires the same administrator account. Diagnostics are available through the widget settings and Diagnose-Readings.ps1.'
Write-Host 'In widget settings, enable CPU temp, GPU temp and/or Fan. Hardware polling pauses when no hardware rows are visible or the widget is hidden/closed.'
Write-Host ('Optional motherboard fan probing: ' + [bool]$EnableMotherboardFanProbing)

# Local, read-only status report for missing live readings.
[CmdletBinding()]
param([switch]$MeasureResources)
$ErrorActionPreference = 'Stop'
$package = Get-AppxPackage -Name 'CyborgVitals' | Select-Object -First 1
if (-not $package) { Write-Host 'Cyborg Vitals is not registered for this Windows user.'; exit 1 }
Write-Host ('Installed widget version: ' + $package.Version)
try {
    [xml]$installedManifest = Get-Content -LiteralPath (Join-Path $package.InstallLocation 'AppxManifest.xml') -Raw
    $foreground = $installedManifest.SelectSingleNode("//*[local-name()='GameBarWidget']/*[local-name()='Window']/*[local-name()='AllowForegroundTransparency']")
    Write-Host ('Foreground transparency permitted by package: ' + [bool]($foreground -and $foreground.InnerText -eq 'true'))
} catch { Write-Host ('Could not read installed transparency flag: ' + $_.Exception.Message) }
$processes = @(Get-Process -Name 'CyborgVitals.Collector' -ErrorAction SilentlyContinue)
Write-Host ('Sensor reader running: ' + ($processes.Count -gt 0))
if ($processes.Count) { Write-Host ('Reader process IDs: ' + (($processes | ForEach-Object { $_.Id }) -join ', ')) }
$folder = Join-Path $env:LOCALAPPDATA ('Packages\' + $package.PackageFamilyName + '\LocalState')
$path = Join-Path $folder 'telemetry.json'
Write-Host ('Data file: ' + $path)
if (Test-Path -LiteralPath $path) {
    try {
        $data = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        Write-Host ('Telemetry age (seconds): ' + [math]::Round(($now - $data.timestampUnixMs) / 1000, 1))
        Write-Host ('CPU usage (%): ' + $data.cpuUsagePercent)
        Write-Host ('RAM used / total GiB: ' + $data.ramUsedGiB + ' / ' + $data.ramTotalGiB)
        Write-Host ('CPU / GPU temperatures C: ' + $data.cpuTemperatureC + ' / ' + $data.gpuTemperatureC)
        Write-Host ('CPU / GPU sensor labels: ' + $data.cpuTemperatureLabel + ' / ' + $data.gpuTemperatureLabel)
        Write-Host ('Hardware reader enabled: ' + $data.hardwareSensorsEnabled)
        Write-Host ('Notification-area icon enabled: ' + $data.trayIconEnabled)
        Write-Host ('Motherboard fan probing enabled: ' + $data.motherboardFanProbingEnabled)
        Write-Host ('Monitoring status: ' + $data.hardwareMonitoringStatus)
        Write-Host ('Sensor driver: ' + $data.sensorDriverStatus)
        Write-Host ('Fan sensor count: ' + @($data.fans).Count)
        Write-Host ('Fan source: ' + $data.fanSource)
        Write-Host ('Fan fallback status: ' + $data.fanStatus)
        if ($data.msiFanRaw) { Write-Host ('MSI Get_Fan raw reply: ' + $data.msiFanRaw) }
        foreach ($fan in @($data.fans)) {
            if ($null -ne $fan) { Write-Host ('  ' + $fan.name + ' [' + $fan.hardwareName + ']: ' + $fan.rpm + ' RPM') }
        }
        Write-Host ('Sensor status/error: ' + $data.sensorError)
        if ($data.PSObject.Properties['hardwareTimestampUnixMs'] -and $null -ne $data.hardwareTimestampUnixMs) {
            Write-Host ('Hardware sample age (seconds): ' + [math]::Round(($now - $data.hardwareTimestampUnixMs) / 1000, 1))
        }
    } catch { Write-Host ('Data read error: ' + $_.Exception.Message) -ForegroundColor Red }
} else { Write-Host 'No telemetry file exists. For temperatures, run Start-Monitor.ps1 -EnableHardwareSensors and accept the reader administrator prompt.' }
$errorLog = Join-Path $folder 'collector-errors.log'
Write-Host 'Current widget monitoring request:'
$demandPath = Join-Path $folder 'reader-demand.json'
if (Test-Path -LiteralPath $demandPath) {
    try {
        $demand = Get-Content -LiteralPath $demandPath -Raw | ConvertFrom-Json
        $age = [math]::Round(([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() - $demand.TimestampUnixMs) / 1000, 1)
        Write-Host ('  Age: ' + $age + ' seconds; CPU temp: ' + $demand.Cpu + '; GPU: ' + $demand.Gpu + '; Fans: ' + $demand.Fans)
    } catch { Write-Host ('  Request read error: ' + $_.Exception.Message) }
} else { Write-Host '  No request file. Open/pin the widget with the desired hardware readings enabled.' }
$uiStatus = Join-Path $folder 'widget-ui-status.txt'
if (Test-Path -LiteralPath $uiStatus) { Write-Host 'Last settings UI event:'; Get-Content -LiteralPath $uiStatus }
$launchStatus = Join-Path $folder 'reader-launch-status.txt'
if (Test-Path -LiteralPath $launchStatus) { Write-Host 'Last automatic reader launch:'; Get-Content -LiteralPath $launchStatus }
$appearancePath = Join-Path $folder 'appearance-status.json'
if (Test-Path -LiteralPath $appearancePath) {
    try {
        $appearance = Get-Content -LiteralPath $appearancePath -Raw | ConvertFrom-Json
        Write-Host 'Appearance readback (last appearance/display-mode change):'
        Write-Host ('  Display mode: ' + $appearance.displayMode + '; pinned: ' + $appearance.pinned)
        Write-Host ('  Saved background opacity: ' + $appearance.configuredOpacityPercent + '%; use Game Bar opacity: ' + $appearance.useGameBarOpacity)
        Write-Host ('  Saved background transparency (100=clear): ' + (100 - $appearance.configuredOpacityPercent) + '%')
        Write-Host ('  Game Bar raw opacity: ' + $appearance.gameBarRequestedOpacityRaw)
        Write-Host ('  Applied background alpha (0=clear): ' + $appearance.appliedBackgroundAlpha + '; backplate visible: ' + $appearance.backplateVisible)
        Write-Host ('  Widget frame background: ' + $appearance.frameBackground)
    } catch { Write-Host ('Appearance readback error: ' + $_.Exception.Message) }
} else { Write-Host 'No appearance readback yet. Version 1.2.0.4 records it after opening the widget or using Clear.' }
try {
    $driver = @(Get-CimInstance -ClassName Win32_SystemDriver -Filter "Name LIKE '%PawnIO%'" -ErrorAction Stop)
    if ($driver.Count) { foreach ($item in $driver) { Write-Host ('PawnIO driver entry: ' + $item.Name + '; state: ' + $item.State) } }
    else { Write-Host 'PawnIO driver entry: not found by Windows driver inventory.' }
} catch { Write-Host ('PawnIO driver inventory query unavailable: ' + $_.Exception.Message) }
if (Test-Path -LiteralPath $errorLog) { Write-Host 'Recent sensor reader errors:'; Get-Content -LiteralPath $errorLog -Tail 15 }
Write-Host 'Inside the widget, press Win+G and click the status dot for the actual widget-side read error.'
if ($MeasureResources) {
    Write-Host 'Sampling only Cyborg Vitals CPU and memory use for five seconds...'
    $tracked = @(Get-Process -Name 'CyborgVitals','CyborgVitals.Collector' -ErrorAction SilentlyContinue)
    $before = @{}
    foreach ($p in $tracked) { try { $before[$p.Id] = $p.TotalProcessorTime.TotalSeconds } catch { } }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds 5
    $elapsed = $clock.Elapsed.TotalSeconds
    $logical = [Environment]::ProcessorCount
    foreach ($p in $tracked) {
        try {
            $p.Refresh()
            if (-not $before.ContainsKey($p.Id) -or $p.HasExited) { continue }
            $cpu = 100 * ($p.TotalProcessorTime.TotalSeconds - $before[$p.Id]) / ($elapsed * $logical)
            Write-Host ('{0} (PID {1}): {2:N2}% total CPU, {3:N1} MiB working set' -f $p.ProcessName,$p.Id,$cpu,($p.WorkingSet64 / 1MB))
        } catch { Write-Host ('Could not sample process ' + $p.Id + ': ' + $_.Exception.Message) }
    }
    Write-Host 'This short sample excludes Game Bar host and kernel-driver overhead; it is not a battery/power measurement.'
}

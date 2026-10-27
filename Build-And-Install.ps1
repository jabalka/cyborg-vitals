# May be run directly or by Setup.exe. Installs for the current Windows user.
[CmdletBinding()]
param([switch]$PreferExistingBuild)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest
try { [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false) } catch { }
$root = $PSScriptRoot
. (Join-Path $root 'Install-Core.ps1')
if ($env:OS -ne 'Windows_NT' -or -not [Environment]::Is64BitOperatingSystem) { throw 'Cyborg Vitals requires 64-bit Windows 11.' }
$dev = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue
if (-not $dev -or -not $dev.PSObject.Properties['AllowDevelopmentWithoutDevLicense'] -or $dev.AllowDevelopmentWithoutDevLicense -ne 1) {
    throw 'Enable Windows Settings > For developers > Developer Mode, then install again.'
}
if (Get-Process -Name 'CyborgVitals.Collector' -ErrorAction SilentlyContinue) {
    throw 'Stop the Cyborg Vitals reader using Stop-Monitor.ps1 or Stop reader in Setup before installing. For an older build, exit its tray icon once.'
}
[xml]$sourceManifest = Get-Content (Join-Path $root 'Widget\Package.appxmanifest') -Raw
$version = [version]$sourceManifest.Package.Identity.Version
$packageOutput = Join-Path $root 'dist\Widget'
$collectorSource = Join-Path $root 'dist\Collector'
$collectorExe = Join-Path $collectorSource 'CyborgVitals.Collector.exe'
$package = Find-CyborgBuiltPackage -Directory $packageOutput -Version $version
$reuse = $PreferExistingBuild -and $null -ne $package -and (Test-Path -LiteralPath $collectorExe)
if ($reuse) {
    Write-Host 'Using the build you already completed.' -ForegroundColor Cyan
} else {
    . (Join-Path $root 'Prerequisites-Core.ps1')
    $studio = Find-CyborgVisualStudio
    if (-not $studio -or -not (Test-Path -LiteralPath $studio.MSBuild) -or -not (Test-Path -LiteralPath $studio.XamlTargets)) {
        throw 'Visual Studio 2022 UWP C# tools are missing. Run Install.ps1 for guided setup or see README.md.'
    }
    $msbuild = $studio.MSBuild
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Include'
    $sdk = Get-ChildItem $sdkRoot -Directory | Where-Object { $_.Name -match '^10\.0\.\d+\.0$' -and [version]$_.Name -ge [version]'10.0.22621.0' } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $sdk) { throw 'Install Windows 11 SDK 10.0.22621.0 or later through Visual Studio Installer.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install a .NET SDK capable of building .NET 8, then open setup again.' }
    Write-Host 'Building the background sensor reader...' -ForegroundColor Cyan
    & dotnet publish (Join-Path $root 'Collector\Collector.csproj') -c Release -r win-x64 --self-contained true -o $collectorSource
    if ($LASTEXITCODE -ne 0) { throw 'Sensor reader build failed. See the log above.' }
    Write-Host 'Building the Game Bar widget...' -ForegroundColor Cyan
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Build-Launcher.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Automatic sensor launcher build failed.' }
    & $msbuild (Join-Path $root 'Widget\Widget.csproj') /restore /t:Build /p:Configuration=Debug /p:Platform=x64 "/p:TargetPlatformVersion=$($sdk.Name)" /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=true /p:UapAppxPackageBuildMode=SideloadOnly "/p:AppxPackageDir=$packageOutput\" /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Widget build failed. See the log above.' }
    $package = Find-CyborgBuiltPackage -Directory $packageOutput -Version $version
}
if ($null -eq $package) { throw "No Cyborg Vitals x64 package for version $version was found under $packageOutput." }
Write-Host ("Completed package: " + $package.Path)

# Install signed Microsoft framework dependencies supplied with this specific build.
$dependencyRoot = Join-Path (Split-Path $package.Path -Parent) 'Dependencies'
if (Test-Path -LiteralPath $dependencyRoot) {
    $dependencies = @(Get-ChildItem -LiteralPath $dependencyRoot -Recurse -File | Where-Object {
        $_.Extension -in '.appx', '.msix' -and $_.FullName -match '[\\/]Dependencies[\\/](x64|neutral)[\\/]'
    })
    foreach ($dependency in $dependencies) {
        $info = Read-CyborgPackageMetadata -Path $dependency.FullName
        $existing = Get-AppxPackage -Name $info.Name | Where-Object {
            [version]$_.Version -ge $info.Version -and $_.Architecture.ToString() -in 'X64', 'Neutral'
        }
        if (-not $existing) {
            Write-Host ("Installing runtime dependency: " + $info.Name)
            Add-AppxPackage -Path $dependency.FullName
        }
    }
}
$installRoot = Join-Path $env:LOCALAPPDATA 'CyborgVitals'
Write-Host 'Extracting the completed package into the application folder...' -ForegroundColor Cyan
$layout = Expand-CyborgBuiltPackage -Package $package -InstallRoot $installRoot
Write-Host ("Registering extracted manifest: " + $layout)
$collectorTarget = Join-Path $installRoot 'dist\Collector'
New-Item -ItemType Directory -Path $collectorTarget -Force | Out-Null
foreach ($item in Get-ChildItem -LiteralPath $collectorSource) { Copy-Item -LiteralPath $item.FullName -Destination $collectorTarget -Recurse -Force }
foreach ($name in 'Start-Monitor.ps1', 'Stop-Monitor.ps1', 'Remove-Widget.ps1', 'Diagnose-Readings.ps1') {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $installRoot $name) -Force
}

try { Add-AppxPackage -Register $layout -ForceApplicationShutdown }
catch { throw "Windows could not register the extracted widget: $_" }
$registered = Get-AppxPackage -Name 'CyborgVitals' | Where-Object { [version]$_.Version -eq $version } | Select-Object -First 1
if (-not $registered) { throw 'Registration returned without the expected package appearing for this user.' }

# Keep runtime files independent of the source/download folder.
$setup = Join-Path $root 'Setup.exe'
if (Test-Path -LiteralPath $setup) {
    Copy-Item -LiteralPath $setup -Destination (Join-Path $installRoot 'Setup.exe') -Force
    $config = Join-Path $root 'Setup.exe.config'
    if (Test-Path -LiteralPath $config) { Copy-Item -LiteralPath $config -Destination (Join-Path $installRoot 'Setup.exe.config') -Force }
    $programs = [Environment]::GetFolderPath('Programs')
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $programs 'Cyborg Vitals.lnk'))
    $shortcut.TargetPath = Join-Path $installRoot 'Setup.exe'
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.Description = 'Open Cyborg Vitals setup and sensor-reader controls'
    $shortcut.Save()
}
@{ Version = $version.ToString(); ManifestPath = $layout; PackageFamilyName = $registered.PackageFamilyName } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installRoot 'install-info.json') -Encoding UTF8
Write-Host ''
Write-Host 'Installation complete. Use Cyborg Vitals in the Start menu, then Win+G > Widget menu > Cyborg Vitals.' -ForegroundColor Green
Write-Host 'Hardware readings can start the reader automatically when the widget opens. Windows may request administrator approval.'

# No elevation, AppX registration, real sensor access or installed files are used.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'Install-Core.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temp = Join-Path ([IO.Path]::GetTempPath()) ('CyborgVitals-InstallerChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
function Assert-Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    Write-Host ('PASS ' + $message)
}
function New-Fixture([string]$Name, [string]$Version, [string]$Architecture, [string]$Filename, [bool]$WithExecutable = $true) {
    $source = Join-Path $temp ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $source | Out-Null
    $xml = '<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"><Identity Name="' + $Name + '" Publisher="CN=CyborgVitals" Version="' + $Version + '" ProcessorArchitecture="' + $Architecture + '"/><Applications><Application Id="App" Executable="CyborgVitals.exe"/></Applications></Package>'
    [IO.File]::WriteAllText((Join-Path $source 'AppxManifest.xml'), $xml)
    if ($WithExecutable) { [IO.File]::WriteAllText((Join-Path $source 'CyborgVitals.exe'), 'fixture, not executable') }
    $path = Join-Path $temp $Filename
    [IO.Compression.ZipFile]::CreateFromDirectory($source, $path)
    return $path
}
try {
    $old = New-Fixture 'CyborgVitals' '1.0.0.0' 'x64' 'old.msix'
    $x86 = New-Fixture 'CyborgVitals' '1.1.0.1' 'x86' 'wrong-arch.msix'
    $correct = New-Fixture 'CyborgVitals' '1.1.0.1' 'x64' 'actual-output.msix'
    $metadata = Read-CyborgPackageMetadata -Path $correct
    Assert-Check ($metadata.Name -eq 'CyborgVitals' -and $metadata.Version -eq [version]'1.1.0.1') 'Package identity is read from the built MSIX'
    $package = Find-CyborgBuiltPackage -Directory $temp -Version ([version]'1.1.0.1')
    Assert-Check ($package.Path -eq $correct) 'Correct version and x64 architecture selected'
    Assert-Check ($null -eq (Find-CyborgBuiltPackage -Directory $temp -Version ([version]'8.0.0.0'))) 'A missing version never falls back to an older build'
    $layout = Expand-CyborgBuiltPackage -Package $package -InstallRoot (Join-Path $temp 'install')
    Assert-Check (Test-Path -LiteralPath $layout) 'Installation works with no Visual Studio AppX staging directory'
    Assert-Check (Test-Path -LiteralPath (Join-Path (Split-Path $layout -Parent) 'CyborgVitals.exe')) 'Widget executable is extracted with its manifest'
    $again = Expand-CyborgBuiltPackage -Package $package -InstallRoot (Join-Path $temp 'install')
    Assert-Check ($again -eq $layout) 'Repeating the same package reuses its existing extracted layout'
    $bad = New-Fixture 'CyborgVitals' '1.1.0.1' 'x64' 'missing-executable.msix' $false
    $rejected = $false
    try { Expand-CyborgBuiltPackage -Package (Read-CyborgPackageMetadata -Path $bad) -InstallRoot (Join-Path $temp 'bad-install') | Out-Null }
    catch { $rejected = $true }
    Assert-Check $rejected 'A package without its executable is rejected before registration'
    Write-Host '7 installer checks passed.'
} finally {
    # Only removes the uniquely generated test fixture directory.
    if (Test-Path -LiteralPath $temp) { [IO.Directory]::Delete($temp, $true) }
}

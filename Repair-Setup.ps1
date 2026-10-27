# Recompile the supplied GUI source locally with Windows' .NET Framework compiler.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT' -or $PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this script with Windows PowerShell: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-Setup.ps1'
}
$source = Join-Path $PSScriptRoot 'Installer\Setup.cs'
$assemblyInfo = Join-Path $PSScriptRoot 'Installer\AssemblyInfo.cs'
if (-not (Test-Path -LiteralPath $source) -or -not (Test-Path -LiteralPath $assemblyInfo)) { throw 'Extract all files from the updated ZIP together before repairing Setup.' }
$compiler = $null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $compiler = & $vswhere -latest -version '[17.0,18.0)' -products '*' -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
}
if (-not $compiler) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'A Windows C# compiler was not found. Keep the command installation path for now and share this output.' }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Core
$references = @(
    [object].Assembly.Location,
    [Uri].Assembly.Location,
    [System.Linq.Enumerable].Assembly.Location,
    [System.Drawing.Color].Assembly.Location,
    [System.Windows.Forms.Application].Assembly.Location
) | Select-Object -Unique
$temporaryExe = Join-Path $PSScriptRoot ('Setup-rebuilt-' + [guid]::NewGuid().ToString('N') + '.exe')
$arguments = @('/nologo', '/noconfig', '/nostdlib+', '/target:winexe', '/platform:anycpu', '/langversion:5',
    ('/out:' + $temporaryExe), ('/win32manifest:' + (Join-Path $PSScriptRoot 'Installer\Setup.manifest')),
    ('/win32icon:' + (Join-Path $PSScriptRoot 'Installer\Setup.ico')))
foreach ($reference in $references) { $arguments += '/reference:' + $reference }
$arguments += $source, $assemblyInfo
Write-Host 'Rebuilding Setup.exe using the compiler already installed on Windows...' -ForegroundColor Cyan
Write-Host $compiler
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed. Share the compiler error above.' }
try {
    Copy-Item -LiteralPath $temporaryExe -Destination (Join-Path $PSScriptRoot 'Setup.exe') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Installer\App.config') -Destination (Join-Path $PSScriptRoot 'Setup.exe.config') -Force
} catch { throw "Close any running Setup process, then repair again. Details: $_" }
finally { if (Test-Path -LiteralPath $temporaryExe) { [IO.File]::Delete($temporaryExe) } }
$installed = Join-Path $env:LOCALAPPDATA 'CyborgVitals'
if (Test-Path -LiteralPath (Join-Path $installed 'Setup.exe')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Setup.exe') -Destination (Join-Path $installed 'Setup.exe') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Setup.exe.config') -Destination (Join-Path $installed 'Setup.exe.config') -Force
}
Write-Host 'Setup rebuilt. Test its launch with Test-SetupLaunch.ps1.' -ForegroundColor Green

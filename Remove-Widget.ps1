# Stop this account's reader before removing its package/data folder.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'CyborgVitals.Collector' -ErrorAction SilentlyContinue) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Stop-Monitor.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Reader did not stop. Follow the message above before removing the widget.' }
}
Get-AppxPackage -Name 'CyborgVitals' | Remove-AppxPackage
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Cyborg Vitals.lnk'
if (Test-Path -LiteralPath $shortcut) { [IO.File]::Delete($shortcut) }
Write-Host 'Cyborg Vitals widget and Start menu shortcut removed.'
Write-Host 'After closing the sensor reader and Setup, you can delete the CyborgVitals folder in Local AppData and the extracted project folder.'

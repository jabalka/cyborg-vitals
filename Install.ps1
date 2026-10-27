# Interactive entry point used by the npm CLI or directly from a source ZIP.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Install-Prerequisites.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Prerequisite check/setup did not finish. Resolve its message before installing.' }
# Prerequisite installers ran in a child process; refresh this process too.
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
if (Get-Process -Name 'CyborgVitals.Collector' -ErrorAction SilentlyContinue) {
    Write-Host 'Stopping the existing Cyborg Vitals reader before updating...'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Stop-Monitor.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'The existing reader could not be stopped. Close it before updating.' }
}
Write-Host 'Close the Cyborg Vitals widget in Win+G before continuing.'
if ([Console]::IsInputRedirected) { throw 'Run this installer in an interactive terminal.' }
[void](Read-Host 'Press Enter when it is closed')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Build-And-Install.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Widget installation failed. Keep the build output above for diagnosis.' }

[CmdletBinding()]
param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Prerequisites-Core.ps1')
function Show-Requirements($state) {
    foreach ($entry in @(
        @('Windows 11 Intel/AMD x64', $state.Platform), @('.NET SDK 8 or newer', $state.DotNet),
        @('Visual Studio 2022 UWP C# tools', $state.Uwp), @('Windows SDK 22621 or newer', $state.Sdk),
        @('Developer Mode', $state.DeveloperMode), @('Xbox Game Bar for this user', $state.GameBar)
    )) {
        $prefix = if ($entry[1]) { '[OK] ' } else { '[MISSING] ' }
        Write-Host ($prefix + $entry[0])
    }
}
function Confirm-Requirement([string]$Message) {
    if ([Console]::IsInputRedirected) { throw 'Prerequisites need an interactive terminal. Run cyborg-vitals install in Command Prompt or Windows Terminal.' }
    (Read-Host ($Message + ' [y/N]')) -match '^(y|yes)$'
}
function Invoke-ToolInstall([string]$Id, [string]$Override) {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { throw 'WinGet is unavailable. Install/update App Installer from Microsoft Store or follow README.md manual setup.' }
    $toolArgs = @('install', '--id', $Id, '--exact', '--source', 'winget', '--interactive')
    if ($Override) { $toolArgs += @('--override', $Override) }
    & winget @toolArgs
    if ($LASTEXITCODE -eq 3010) { throw 'The prerequisite installer requires a Windows restart. Restart, then run the same install command again.' }
    if ($LASTEXITCODE -ne 0) { throw "Installation of $Id did not finish successfully (exit $LASTEXITCODE). Follow README.md and retry." }
}
$state = Get-CyborgPrerequisites
Show-Requirements $state
if (-not $state.Platform) { throw 'This source installer requires Windows 11 on an Intel/AMD x64 PC.' }
if ($CheckOnly) {
    if (-not ($state.DotNet -and $state.Uwp -and $state.Sdk -and $state.DeveloperMode -and $state.GameBar)) { exit 1 }
    exit 0
}
$plan = Get-CyborgToolPlan $state
if ($plan.Components.Count) {
    Write-Host 'Requested Visual Studio components (required dependencies are included by Microsoft):'
    $plan.Components | ForEach-Object { Write-Host ('  ' + $_) }
    Write-Host 'Visual Studio build tools can use several GB. Recommended/optional C++, Copilot and IntelliCode packages are not requested.'
    if (-not (Confirm-Requirement 'Install only these missing tool categories, or add them to your existing Visual Studio 2022?')) { throw 'Tool installation declined. Nothing else will be installed. Follow README.md manual setup when ready.' }
    $add = ($plan.Components | ForEach-Object { '--add ' + $_ }) -join ' '
    if ($plan.ModifyVisualStudio) {
        $setup = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\setup.exe'
        $arguments = 'modify --installPath "' + $state.VisualStudio.Root + '" --passive --norestart ' + $add
        $process = Start-Process -FilePath $setup -ArgumentList $arguments -Verb RunAs -WorkingDirectory $env:TEMP -Wait -PassThru
        if ($process.ExitCode -eq 3010) { throw 'Restart Windows before continuing, then rerun the install command.' }
        if ($process.ExitCode -ne 0) { throw ('Visual Studio modification failed: ' + $process.ExitCode) }
    } else {
        Write-Host 'Visual Studio Community has license eligibility requirements. Select No and install your licensed edition manually if needed.'
        if (-not (Confirm-Requirement 'Use Microsoft Visual Studio 2022 Community?')) { throw 'Install your licensed Visual Studio 2022 edition with the README components, then retry.' }
        Invoke-ToolInstall 'Microsoft.VisualStudio.2022.Community' ('--passive --norestart ' + $add)
    }
    $state = Get-CyborgPrerequisites
}
# Visual Studio may have installed a suitable SDK; do not install another needlessly.
if (-not $state.DotNet) {
    if (-not (Confirm-Requirement 'Install Microsoft .NET 8 SDK? A newer existing SDK is also sufficient.')) { throw '.NET SDK installation declined.' }
    Invoke-ToolInstall 'Microsoft.DotNet.SDK.8' ''
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = $machinePath + ';' + $userPath
    $state = Get-CyborgPrerequisites
}
if (-not $state.DeveloperMode) {
    if (-not (Confirm-Requirement 'Open Windows Developer settings? Enable Developer Mode there to register this local build.')) { throw 'Developer Mode is required for this development build.' }
    Start-Process 'ms-settings:developers'
    [void](Read-Host 'After enabling Developer Mode, return here and press Enter')
}
$state = Get-CyborgPrerequisites
if (-not $state.GameBar) {
    if (-not (Confirm-Requirement 'Open the official Game Bar Store page so you can install it?')) { throw 'Game Bar is required to display the widget.' }
    Start-Process 'ms-windows-store://pdp/?ProductId=9NZKPSTSNW4P'
    [void](Read-Host 'After installing Game Bar, return here and press Enter')
}
$state = Get-CyborgPrerequisites
Show-Requirements $state
if (-not ($state.DotNet -and $state.Uwp -and $state.Sdk -and $state.DeveloperMode -and $state.GameBar)) {
    throw 'Some requirements are still missing. Follow README.md manual setup, reopen the terminal and retry. No widget registration was attempted.'
}
Write-Host 'Prerequisites ready.' -ForegroundColor Green

# Pure planning fixtures. Never installs tools, changes registry or opens apps.
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'Prerequisites-Core.ps1')
function Assert([bool]$condition, [string]$message) { if (-not $condition) { throw $message }; Write-Host ('PASS ' + $message) }
$ready = [pscustomobject]@{ DotNet = $true; Uwp = $true; Sdk = $true; VisualStudio = [pscustomobject]@{Root='fixture'} }
$plan = Get-CyborgToolPlan $ready
Assert (-not $plan.InstallDotNet -and $plan.Components.Count -eq 0) 'Installed tools do not request duplicates'
$ready.DotNet = $false
$plan = Get-CyborgToolPlan $ready
Assert ($plan.InstallDotNet -and $plan.Components.Count -eq 0) 'Missing SDK requests .NET without unrelated workloads'
$ready.DotNet = $true; $ready.Uwp = $false
$plan = Get-CyborgToolPlan $ready
Assert ($plan.ModifyVisualStudio -and $plan.Components -contains 'Microsoft.VisualStudio.ComponentGroup.UWP.Support') 'Existing Visual Studio is modified for missing UWP tools'
Assert ($plan.Components -notcontains 'Microsoft.VisualStudio.Component.Windows11SDK.26100') 'Existing Windows SDK is retained'
$ready.Uwp = $true; $ready.Sdk = $false
$plan = Get-CyborgToolPlan $ready
Assert ($plan.Components.Count -eq 1 -and $plan.Components[0] -eq 'Microsoft.VisualStudio.Component.Windows11SDK.26100') 'Only missing Windows SDK is requested'
$ready.Uwp = $false; $ready.VisualStudio = $null
$plan = Get-CyborgToolPlan $ready
Assert (-not $plan.ModifyVisualStudio -and $plan.Components.Count -eq 3) 'Fresh PC requests required workload, UWP support and one SDK'
Assert (-not (@($plan.Components | Where-Object { $_ -match 'VC\.|Copilot|IntelliCode|Xamarin' }).Count)) 'No optional C++, Copilot, IntelliCode or Xamarin requests'
if ($env:OS -ne 'Windows_NT') {
    $platform = Get-CyborgPrerequisites
    Assert (-not $platform.Platform -and -not $platform.DotNet -and -not $platform.GameBar) 'Non-Windows discovery returns an unsupported report without modifying the host'
}
Write-Host 'Prerequisite planning/discovery checks passed.'

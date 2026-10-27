# Discovery and plans only. Dot-source for installer or read-only checks.
function Find-CyborgVisualStudio {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) { return $null }
    $instances = @(& $vswhere -all -version '[17.0,18.0)' -products '*' -format json | ConvertFrom-Json)
    $fallback = $null
    foreach ($instance in $instances) {
        $root = $instance.installationPath
        if (-not $root) { continue }
        $item = [pscustomobject]@{
            Root = $root
            MSBuild = Join-Path $root 'MSBuild\Current\Bin\MSBuild.exe'
            XamlTargets = Join-Path $root 'MSBuild\Microsoft\WindowsXaml\v17.0\Microsoft.Windows.UI.Xaml.CSharp.targets'
        }
        if ((Test-Path -LiteralPath $item.MSBuild) -and (Test-Path -LiteralPath $item.XamlTargets)) { return $item }
        if (-not $fallback -and $instance.productId -notmatch 'BuildTools') { $fallback = $item }
    }
    return $fallback
}
function Get-CyborgPrerequisites {
    $cyborgWindows = $env:OS -eq 'Windows_NT'
    $x64 = $env:PROCESSOR_ARCHITECTURE -eq 'AMD64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'AMD64'
    $vs = $null; $sdk = $null; $dotnet = $false; $developer = $false; $gameBar = $false
    if ($cyborgWindows) {
        $vs = Find-CyborgVisualStudio
        $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Include'
        if (Test-Path -LiteralPath $sdkRoot) {
            $sdk = Get-ChildItem -LiteralPath $sdkRoot -Directory | Where-Object { $_.Name -match '^10\.0\.\d+\.0$' -and [version]$_.Name -ge [version]'10.0.22621.0' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        }
        $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $dotnetCommand) {
            $fixedDotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
            if (Test-Path -LiteralPath $fixedDotnet) { $dotnetCommand = Get-Item -LiteralPath $fixedDotnet }
        }
        if ($dotnetCommand) {
            $dotnetPath = if ($dotnetCommand -is [IO.FileInfo]) { $dotnetCommand.FullName } else { $dotnetCommand.Source }
            $dotnet = @(& $dotnetPath --list-sdks 2>$null | Where-Object { $_ -match '^([89]|[1-9][0-9]+)\.' }).Count -gt 0
        }
        $mode = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue
        $developer = $null -ne $mode -and $null -ne $mode.PSObject.Properties['AllowDevelopmentWithoutDevLicense'] -and $mode.AllowDevelopmentWithoutDevLicense -eq 1
        $gameBar = $null -ne (Get-AppxPackage -Name 'Microsoft.XboxGamingOverlay' -ErrorAction SilentlyContinue | Select-Object -First 1)
    }
    [pscustomobject]@{
        Platform = $cyborgWindows -and $x64 -and [Environment]::OSVersion.Version.Build -ge 22000
        DotNet = $dotnet
        VisualStudio = $vs
        Uwp = $null -ne $vs -and (Test-Path -LiteralPath $vs.MSBuild) -and (Test-Path -LiteralPath $vs.XamlTargets)
        Sdk = $null -ne $sdk
        SdkVersion = if ($sdk) { $sdk.Name } else { $null }
        DeveloperMode = $developer
        GameBar = $gameBar
    }
}
function Get-CyborgToolPlan($State) {
    # Add only missing tool categories. The executor asks before making changes.
    $components = @()
    if (-not $State.Uwp) { $components += 'Microsoft.VisualStudio.Workload.Universal', 'Microsoft.VisualStudio.ComponentGroup.UWP.Support' }
    if (-not $State.Sdk) { $components += 'Microsoft.VisualStudio.Component.Windows11SDK.26100' }
    [pscustomobject]@{ InstallDotNet = -not $State.DotNet; Components = $components; ModifyVisualStudio = $null -ne $State.VisualStudio }
}

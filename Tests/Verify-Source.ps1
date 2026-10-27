[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$failures = 0
Get-ChildItem $root -Filter '*.ps1' -Recurse | ForEach-Object {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    foreach ($item in $parseErrors) { Write-Host "$($_.Name): $($item.Message)"; $failures++ }
}
Get-ChildItem $root -Recurse -File | Where-Object { $_.Extension -in '.xaml', '.csproj', '.appxmanifest', '.manifest', '.xml', '.config' } | ForEach-Object {
    try { [xml](Get-Content $_.FullName -Raw) | Out-Null }
    catch { Write-Host "$($_.Name): $_"; $failures++ }
}
[xml]$manifest = Get-Content (Join-Path $root 'Widget\Package.appxmanifest') -Raw
$ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace('p', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('mp', 'http://schemas.microsoft.com/appx/2014/phone/manifest')
$ns.AddNamespace('uap3', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3')
$ns.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10')
$ns.AddNamespace('rescap', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities')
$extensions = @($manifest.SelectNodes('//uap3:AppExtension', $ns))
if (@($extensions | Group-Object { $_.Id } | Where-Object Count -gt 1).Count) {
    Write-Host 'Game Bar extension IDs must be unique.'; $failures++
}
$parent = $extensions | Where-Object { $_.Id -eq 'Vitals' } | Select-Object -First 1
if (-not $parent -or $extensions.Count -ne 1 -or $parent.Properties.GameBarWidget.Type -ne 'Standard') {
    Write-Host 'Only the monitoring widget should be registered; settings use its expanded view.'; $failures++
}
if ($parent.Properties.GameBarWidget.Window.AllowForegroundTransparency -ne 'true') {
    Write-Host 'The widget must opt in to Game Bar foreground transparency.'; $failures++
}
$bridge = $manifest.SelectSingleNode("//desktop:Extension[@Category='windows.fullTrustProcess']", $ns)
if (-not $bridge -or $bridge.Executable -ne 'Bridge\CyborgVitals.Launcher.exe' -or
    -not (Test-Path -LiteralPath (Join-Path $root 'Widget\Bridge\CyborgVitals.Launcher.exe'))) {
    Write-Host 'The automatic reader launcher must be declared and included in the package.'; $failures++
}
foreach ($capability in 'runFullTrust','allowElevation') {
    if (-not $manifest.SelectSingleNode("/p:Package/p:Capabilities/rescap:Capability[@Name='$capability']", $ns)) {
        Write-Host ('Missing launcher capability: ' + $capability); $failures++
    }
}
$phone = $manifest.SelectSingleNode('/p:Package/mp:PhoneIdentity', $ns)
if (-not $phone) { Write-Host 'Required UWP PhoneIdentity metadata is missing.'; $failures++ }
else {
    $productId = [guid]::Empty
    $publisherId = [guid]::Empty
    if (-not [guid]::TryParse($phone.GetAttribute('PhoneProductId'), [ref]$productId)) { $failures++ }
    if (-not [guid]::TryParse($phone.GetAttribute('PhonePublisherId'), [ref]$publisherId)) { $failures++ }
}
if ($failures) { throw "$failures syntax/XML/manifest checks failed." }
Write-Host 'PowerShell syntax and XML checks passed.' -ForegroundColor Green
Write-Host 'For CPU/sensor edge-case checks, run: dotnet run --project .\Tests\MetricsChecks.csproj'

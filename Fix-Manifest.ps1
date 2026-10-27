# Run once in an extracted pre-fix copy if its manifest reports APPX1673.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'Widget\Package.appxmanifest'
$doc = New-Object System.Xml.XmlDocument
$doc.PreserveWhitespace = $true
$doc.Load($path)
$root = $doc.DocumentElement
$phoneNamespace = 'http://schemas.microsoft.com/appx/2014/phone/manifest'
if ($root.GetNamespaceOfPrefix('mp') -and $root.GetNamespaceOfPrefix('mp') -ne $phoneNamespace) {
    throw 'The manifest already uses mp for another namespace; it was not changed.'
}
if (-not $root.GetNamespaceOfPrefix('mp')) {
    $attribute = $doc.CreateAttribute('xmlns', 'mp', 'http://www.w3.org/2000/xmlns/')
    $attribute.Value = $phoneNamespace
    $root.Attributes.Append($attribute) | Out-Null
}
$ignored = @($root.GetAttribute('IgnorableNamespaces') -split '\s+' | Where-Object { $_ })
if ($ignored -notcontains 'mp') { $root.SetAttribute('IgnorableNamespaces', (($ignored + 'mp') -join ' ')) }
$ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
$ns.AddNamespace('p', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('mp', $phoneNamespace)
if (-not $doc.SelectSingleNode('/p:Package/mp:PhoneIdentity', $ns)) {
    $identity = $doc.SelectSingleNode('/p:Package/p:Identity', $ns)
    if (-not $identity) { throw 'The manifest has no package Identity. It was not changed.' }
    $phone = $doc.CreateElement('mp', 'PhoneIdentity', $phoneNamespace)
    $phone.SetAttribute('PhoneProductId', '0b685afe-097c-44b9-986c-4430ef57f6e0')
    $phone.SetAttribute('PhonePublisherId', '00000000-0000-0000-0000-000000000000')
    $root.InsertAfter($phone, $identity) | Out-Null
}
$doc.Save($path)
Write-Host 'PhoneIdentity metadata is present. Run Build-And-Install.ps1 again.' -ForegroundColor Green

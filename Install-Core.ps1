# Package inspection/extraction shared by installation and fixture checks.
# Functions do not register packages or change machine settings.
function Read-CyborgPackageMetadata {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$Path)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -ieq 'AppxManifest.xml' } | Select-Object -First 1
        if (-not $entry) { throw "The package has no AppxManifest.xml: $Path" }
        $reader = New-Object IO.StreamReader($entry.Open())
        try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $ns = New-Object Xml.XmlNamespaceManager($document.NameTable)
        $ns.AddNamespace('p', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
        $identity = $document.SelectSingleNode('/p:Package/p:Identity', $ns)
        if (-not $identity) { throw "The package has no Identity: $Path" }
        [pscustomobject]@{
            Path = [IO.Path]::GetFullPath($Path)
            Name = $identity.GetAttribute('Name')
            Publisher = $identity.GetAttribute('Publisher')
            Version = [version]$identity.GetAttribute('Version')
            Architecture = $identity.GetAttribute('ProcessorArchitecture')
            Manifest = $document
        }
    } finally { $archive.Dispose() }
}

function Find-CyborgBuiltPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$Directory, [Parameter(Mandatory=$true)][version]$Version)
    if (-not (Test-Path -LiteralPath $Directory)) { return $null }
    $files = @(Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object {
        $_.Extension -in '.msix', '.appx' -and $_.FullName -notmatch '[\\/]Dependencies[\\/]'
    } | Sort-Object LastWriteTimeUtc -Descending)
    foreach ($file in $files) {
        $metadata = Read-CyborgPackageMetadata -Path $file.FullName
        if ($metadata.Name -eq 'CyborgVitals' -and $metadata.Publisher -eq 'CN=CyborgVitals' -and
            $metadata.Version -eq $Version -and $metadata.Architecture -eq 'x64') { return $metadata }
    }
    return $null
}

function Expand-CyborgBuiltPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Package, [Parameter(Mandatory=$true)][string]$InstallRoot)
    if ($Package.Name -ne 'CyborgVitals' -or $Package.Publisher -ne 'CN=CyborgVitals' -or $Package.Architecture -ne 'x64') {
        throw 'This is not a Cyborg Vitals x64 development package.'
    }
    $hash = (Get-FileHash -LiteralPath $Package.Path -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant()
    $directory = Join-Path $InstallRoot ("Widget\{0}-{1}" -f $Package.Version, $hash)
    $layout = Join-Path $directory 'AppxManifest.xml'
    if (-not (Test-Path -LiteralPath $layout)) {
        if (Test-Path -LiteralPath $directory) { throw "An incomplete extraction exists at $directory. Keep its files for diagnosis and use a fresh install folder." }
        New-Item -ItemType Directory -Path (Split-Path $directory -Parent) -Force | Out-Null
        [IO.Compression.ZipFile]::ExtractToDirectory($Package.Path, $directory)
    }
    if (-not (Test-Path -LiteralPath $layout)) { throw 'The extracted package is missing AppxManifest.xml.' }
    [xml]$manifest = Get-Content -LiteralPath $layout -Raw
    $ns = New-Object Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace('p', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $identity = $manifest.SelectSingleNode('/p:Package/p:Identity', $ns)
    if (-not $identity -or $identity.GetAttribute('Name') -ne 'CyborgVitals' -or
        [version]$identity.GetAttribute('Version') -ne $Package.Version) { throw 'The extracted package identity does not match the selected build.' }
    $app = $manifest.SelectSingleNode('/p:Package/p:Applications/p:Application[@Id="App"]', $ns)
    if (-not $app) { throw 'The extracted package has no widget application.' }
    $exe = Join-Path $directory $app.GetAttribute('Executable')
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "The extracted widget executable is missing: $exe" }
    return $layout
}

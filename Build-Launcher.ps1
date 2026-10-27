[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT' -or $PSVersionTable.PSEdition -ne 'Desktop') { throw 'Use Windows PowerShell to build the launcher.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler is missing.' }
$output = Join-Path $PSScriptRoot 'Widget\Bridge\CyborgVitals.Launcher.exe'
New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force | Out-Null
$arguments = @('/nologo','/noconfig','/nostdlib+','/target:winexe','/platform:anycpu','/langversion:5',
    ('/out:' + $output), ('/win32manifest:' + (Join-Path $PSScriptRoot 'Launcher\Launcher.manifest')),
    ('/reference:' + [object].Assembly.Location), ('/reference:' + [Uri].Assembly.Location),
    (Join-Path $PSScriptRoot 'Launcher\Program.cs'), (Join-Path $PSScriptRoot 'Shared\LaunchPolicy.cs'))
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'The sensor launcher did not compile. Keep the error above.' }
Write-Host 'Sensor launcher built using the Windows compiler already installed.'

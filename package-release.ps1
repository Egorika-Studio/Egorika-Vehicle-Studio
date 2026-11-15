param([Parameter(Mandatory)][string]$CodeWalkerDirectory)
$ErrorActionPreference = 'Stop'
$version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
$stage = Join-Path $PSScriptRoot ("dist\v" + $version)
New-Item -ItemType Directory -Force $stage | Out-Null
$scratch = Join-Path $stage ('build-' + [guid]::NewGuid().ToString('N'))
$portable = Join-Path $scratch 'portable'
New-Item -ItemType Directory -Force $portable | Out-Null

& (Join-Path $PSScriptRoot 'build.ps1') -CodeWalkerDirectory $CodeWalkerDirectory `
    -OutputDirectory $scratch -ExecutableName 'EgorikaVehicleStudio.exe' -Version $version

$exe = Join-Path $scratch 'EgorikaVehicleStudio.exe'
Copy-Item -LiteralPath $exe -Destination $stage
foreach ($file in 'EgorikaVehicleStudio.exe', 'SharpDX.dll', 'SharpDX.Mathematics.dll') {
    Copy-Item -LiteralPath (Join-Path $scratch $file) -Destination $portable
}
foreach ($file in 'LICENSE', 'SharpDX-LICENSE.txt', 'README.md') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $portable
}
$zip = Join-Path $stage 'EgorikaVehicleStudio-Windows-x64.zip'
Compress-Archive -LiteralPath (Get-ChildItem $portable -File).FullName -DestinationPath $zip -Force
Get-Item -LiteralPath (Join-Path $stage 'EgorikaVehicleStudio.exe'), $zip

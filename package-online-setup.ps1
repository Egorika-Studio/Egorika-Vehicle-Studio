param(
    [Parameter(Mandatory)][string]$PackageZip,
    [string]$Version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim(),
    [Parameter(Mandatory)][string]$InnoSetupCompiler
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Version must be MAJOR.MINOR.PATCH.'
}
$package = Get-Item -LiteralPath $PackageZip
$hash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
$url = "https://github.com/Egorika-Studio/Egorika-Vehicle-Studio/releases/download/payload-v$Version/EgorikaVehicleStudio-Windows-x64.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $sizes = foreach ($name in 'EgorikaVehicleStudio.exe', 'SharpDX.dll', 'SharpDX.Mathematics.dll') {
        $entry = $archive.GetEntry($name)
        if (!$entry -or $entry.Length -eq 0) { throw "Missing or empty package entry: $name" }
        $entry.Length
    }
    $thumbnailBytes = ($archive.Entries | Where-Object { $_.FullName -match '^thumbnails[\\/][^\\/]+\.png$' } |
        Measure-Object -Property Length -Sum).Sum
    if (!$thumbnailBytes) { throw 'The package is missing base-vehicle thumbnails.' }
}
finally {
    $archive.Dispose()
}

$packaging = Join-Path $PSScriptRoot 'packaging'
$assets = Join-Path $packaging 'setup-assets'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('vehicle-setup-build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$engine = Join-Path $scratch 'VehicleStudioInstallEngine.exe'
$outputName = 'EgorikaVehicleStudio-OnlineSetup-Windows-x64.exe'
$bootstrap = Join-Path $scratch $outputName
try {
    & $InnoSetupCompiler "/O$scratch" "/DAppVersion=$Version" "/DOnlinePackageUrl=$url" `
        "/DOnlinePackageHash=$hash" "/DOnlineExeSize=$($sizes[0])" "/DOnlineSharpDXSize=$($sizes[1])" `
        "/DOnlineMathSize=$($sizes[2])" "/DOnlineThumbnailSize=$thumbnailBytes" (Join-Path $packaging 'vehicle-studio.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Online installation engine compilation failed.' }

    $wpfReferences = @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll') |
        ForEach-Object { '/r:' + (Join-Path $framework "WPF\$_") }
    & (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 "/out:$bootstrap" `
        "/win32icon:$PSScriptRoot\Egorika.ico" @wpfReferences "/r:$framework\System.Xaml.dll" `
        "/r:$framework\System.Windows.Forms.dll" "/resource:$engine,engine" `
        "/resource:$packaging\OnlineSetup.xaml,layout" "/resource:$assets\egorika-setup-wordmark.png,wordmark" `
        "/resource:$assets\SourceSans3-Regular.ttf,regular" "/resource:$assets\SourceSans3-Light.ttf,light" `
        "/resource:$assets\LICENSE.md,SourceSans3-License.md" (Join-Path $packaging 'OnlineSetup.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Online installer interface compilation failed.' }
    Copy-Item -LiteralPath $bootstrap -Destination $package.DirectoryName -Force
}
finally {
    foreach ($file in $engine, $bootstrap) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $scratch
}
Get-Item -LiteralPath (Join-Path $package.DirectoryName $outputName)

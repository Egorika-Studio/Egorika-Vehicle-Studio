param(
    [string]$CodeWalkerDirectory,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'),
    [string]$ExecutableName = 'YftViewer.exe',
    [string]$Version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
)
$ErrorActionPreference = 'Stop'
if (!$CodeWalkerDirectory) {
    $running = Get-Process '*CodeWalker*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($running.Path) { $CodeWalkerDirectory = Split-Path $running.Path }
}
if (!$CodeWalkerDirectory -or !(Test-Path (Join-Path $CodeWalkerDirectory 'CodeWalker.Core.dll'))) {
    throw 'Pass -CodeWalkerDirectory with the folder containing CodeWalker.Core.dll.'
}
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $out | Out-Null
$dependencies = Get-ChildItem $CodeWalkerDirectory -File | Where-Object { $_.Name -eq 'CodeWalker.Core.dll' -or $_.Name -like 'SharpDX*.dll' }
foreach ($dependency in $dependencies) {
    $existing = Get-Item (Join-Path $out $dependency.Name) -ErrorAction SilentlyContinue
    if (!$existing -or $existing.Length -ne $dependency.Length -or
        $existing.LastWriteTimeUtc -ne $dependency.LastWriteTimeUtc) {
        Copy-Item -LiteralPath $dependency.FullName -Destination $out
    }
}
Copy-Item (Join-Path $CodeWalkerDirectory 'Notice.txt') $out
$references = @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/r:' + (Join-Path $framework "WPF\$_") }
$references += "/r:$framework\System.IO.Compression.dll", "/r:$framework\System.IO.Compression.FileSystem.dll", "/r:$framework\System.Windows.Forms.dll", "/r:$framework\System.Drawing.dll", "/r:$framework\System.Xml.Linq.dll", "/r:$framework\System.Xaml.dll", "/r:$framework\System.Web.Extensions.dll", "/r:$framework\netstandard.dll"
$references += $dependencies | ForEach-Object { '/r:' + $_.FullName }
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'Version must be MAJOR.MINOR.PATCH.' }
$versionSource = Join-Path $out ('AssemblyVersion-' + [guid]::NewGuid() + '.cs')
try {
    Set-Content -LiteralPath $versionSource -Encoding ascii "[assembly: System.Reflection.AssemblyVersion(""$Version.0"")]"
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 "/win32icon:$PSScriptRoot\Egorika.ico" "/resource:$PSScriptRoot\Egorika.png,Egorika.png" "/out:$out\$ExecutableName" "/resource:$PSScriptRoot\PaintPalette.tsv,PaintPalette.tsv" "/resource:$PSScriptRoot\Translations.txt,Translations.txt" @references (Join-Path $PSScriptRoot 'Vehicle.cs') (Join-Path $PSScriptRoot 'Viewer.cs') (Join-Path $PSScriptRoot 'GameTextures.cs') (Join-Path $PSScriptRoot 'EnvironmentScene.cs') (Join-Path $PSScriptRoot 'AmbientOcclusion.cs') (Join-Path $PSScriptRoot 'TuningEditor.cs') (Join-Path $PSScriptRoot 'TuningWorkspace.cs') (Join-Path $PSScriptRoot 'StudioTheme.cs') (Join-Path $PSScriptRoot 'Ui.cs') (Join-Path $PSScriptRoot 'VehicleCatalog.cs') (Join-Path $PSScriptRoot 'StudioWorkspace.cs') (Join-Path $PSScriptRoot 'StudioProjects.cs') (Join-Path $PSScriptRoot 'VehicleCatalogRow.cs') (Join-Path $PSScriptRoot 'StudioUpdate.cs') (Join-Path $PSScriptRoot 'StudioDependencies.cs') $versionSource
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
}
finally {
    Remove-Item -LiteralPath $versionSource -ErrorAction SilentlyContinue
}
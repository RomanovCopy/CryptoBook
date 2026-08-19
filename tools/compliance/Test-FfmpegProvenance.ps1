[CmdletBinding()]
param(
    [string] $PackageRoot = (Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) `
        '.nuget\packages\sdcb.ffmpeg.runtime.windows-x64\7.1.0'),
    [string] $ManifestPath = (Join-Path $PSScriptRoot '..\..\compliance\ffmpeg\package-manifest.json')
)

$ErrorActionPreference = 'Stop'
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$ManifestPath = [IO.Path]::GetFullPath($ManifestPath)
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$sourcePinsPath = Join-Path (Split-Path -Parent $ManifestPath) 'source-pins.json'
$recipePath = Join-Path (Split-Path -Parent $ManifestPath) `
    'btbn-win64-gpl-shared-7.1.Dockerfile'

$nupkg = Join-Path $PackageRoot 'sdcb.ffmpeg.runtime.windows-x64.7.1.0.nupkg'
if (-not (Test-Path -LiteralPath $nupkg -PathType Leaf)) {
    throw "NuGet archive was not found: $nupkg"
}

function Assert-Equal {
    param([string] $Label, $Actual, $Expected)
    if ($Actual -cne $Expected) {
        throw "$Label mismatch.`nExpected: $Expected`nActual:   $Actual"
    }
}

function Get-NormalizedTextSha256 {
    param([string] $Path)

    $content = (Get-Content -LiteralPath $Path -Raw).Replace("`r`n", "`n")
    $bytes = [Text.Encoding]::UTF8.GetBytes($content)
    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($bytes))
}

$archive = Get-Item -LiteralPath $nupkg
Assert-Equal 'NuGet package size' ([long] $archive.Length) ([long] $manifest.package.size)
Assert-Equal 'NuGet package SHA-256' `
    (Get-FileHash -LiteralPath $nupkg -Algorithm SHA256).Hash `
    ([string] $manifest.package.sha256)

$sha512 = [Security.Cryptography.SHA512]::Create()
try {
    $stream = [IO.File]::OpenRead($nupkg)
    try {
        $archiveSha512 = [Convert]::ToBase64String($sha512.ComputeHash($stream))
    }
    finally {
        $stream.Dispose()
    }
}
finally {
    $sha512.Dispose()
}
Assert-Equal 'NuGet package SHA-512' $archiveSha512 ([string] $manifest.package.sha512)

$sourcePins = Get-Content -LiteralPath $sourcePinsPath -Raw | ConvertFrom-Json
Assert-Equal 'BtbN generated recipe SHA-256' `
    (Get-NormalizedTextSha256 -Path $recipePath) `
    ([string] $sourcePins.recipe.generatedDockerfileSha256)
$recipeConfigureLine = Get-Content -LiteralPath $recipePath |
    Where-Object { $_ -match '^\s*FF_CONFIGURE="(?<value>.+)" \\$' } |
    Select-Object -Last 1
if (-not $recipeConfigureLine -or
    $recipeConfigureLine -notmatch '^\s*FF_CONFIGURE="(?<value>.+)" \\$') {
    throw 'The generated BtbN FF_CONFIGURE line was not found.'
}
$recipeConfiguration = $Matches.value
if (-not ([string] $manifest.nativeBuild.configuration).Contains($recipeConfiguration)) {
    throw 'The generated BtbN feature configuration does not match the DLL configuration.'
}

$nativeDirectory = Join-Path $PackageRoot 'runtimes\win-x64\native'
$runtimeInfo = @(& (Join-Path $PSScriptRoot 'Get-FfmpegBuildInfo.ps1') `
    -NativeDirectory $nativeDirectory)
$runtimeByFile = @{}
foreach ($item in $runtimeInfo) { $runtimeByFile[$item.File] = $item }

foreach ($expected in $manifest.libraries) {
    $path = Join-Path $nativeDirectory $expected.file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Native library was not found: $path"
    }

    $file = Get-Item -LiteralPath $path
    Assert-Equal "$($expected.file) size" ([long] $file.Length) ([long] $expected.size)
    Assert-Equal "$($expected.file) SHA-256" `
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash `
        ([string] $expected.sha256)
    Assert-Equal "$($expected.file) product version" `
        ([Diagnostics.FileVersionInfo]::GetVersionInfo($path).ProductVersion) `
        ([string] $manifest.nativeBuild.embeddedProductVersion)

    $actual = $runtimeByFile[$expected.file]
    if ($null -eq $actual) { throw "No exported metadata was read from $($expected.file)" }
    Assert-Equal "$($expected.file) API version" $actual.Version ([string] $expected.apiVersion)
    Assert-Equal "$($expected.file) license" $actual.License `
        ([string] $manifest.nativeBuild.licenseReportedByLibraries)
    Assert-Equal "$($expected.file) configuration" $actual.Configuration `
        ([string] $manifest.nativeBuild.configuration)
}

[pscustomobject]@{
    Package = "$($manifest.package.id) $($manifest.package.version)"
    Libraries = $manifest.libraries.Count
    FfmpegCommit = $manifest.nativeBuild.ffmpegCommit
    RecipeCommit = $manifest.nativeBuild.buildRecipeCommit
    DeclaredSourcePins = $sourcePins.declaredSourcePinCount
    Status = 'Verified'
}

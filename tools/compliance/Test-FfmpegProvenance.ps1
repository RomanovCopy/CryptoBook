[CmdletBinding()]
param(
    [string] $PackageRoot,
    [string] $ManifestPath = (Join-Path $PSScriptRoot '..\..\compliance\ffmpeg\package-manifest.json')
)

$ErrorActionPreference = 'Stop'
$ManifestPath = [IO.Path]::GetFullPath($ManifestPath)
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$packageIdLower = ([string] $manifest.package.id).ToLowerInvariant()
$packageVersion = [string] $manifest.package.version

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) `
        ".nuget\packages\$packageIdLower\$packageVersion"
}
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$packageFileName = "$packageIdLower.$packageVersion.nupkg"
$nupkg = Join-Path $PackageRoot $packageFileName
if (-not (Test-Path -LiteralPath $nupkg -PathType Leaf)) {
    throw "Restored runtime package was not found: $nupkg"
}

function Assert-Equal {
    param([string] $Label, $Actual, $Expected)
    if ($Actual -cne $Expected) {
        throw "$Label mismatch.`nExpected: $Expected`nActual:   $Actual"
    }
}

$archive = Get-Item -LiteralPath $nupkg
Assert-Equal 'NuGet package size' ([long] $archive.Length) ([long] $manifest.package.size)
$packageSha256 = (Get-FileHash -LiteralPath $nupkg -Algorithm SHA256).Hash
Assert-Equal 'NuGet package SHA-256' $packageSha256 ([string] $manifest.package.sha256)

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

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $ManifestPath) '..\..'))
$localPackage = Join-Path `
    $repositoryRoot `
    "third_party\nuget\$($manifest.package.id).$packageVersion.nupkg"
if (-not (Test-Path -LiteralPath $localPackage -PathType Leaf)) {
    throw "Pinned local-feed package was not found: $localPackage"
}
Assert-Equal 'Local-feed package SHA-256' `
    (Get-FileHash -LiteralPath $localPackage -Algorithm SHA256).Hash `
    $packageSha256

$zip = [IO.Compression.ZipFile]::OpenRead($nupkg)
try {
    $nuspecEntry = $zip.Entries |
        Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
    if (-not $nuspecEntry) { throw 'The runtime package does not contain a nuspec.' }
    $reader = [IO.StreamReader]::new($nuspecEntry.Open())
    try { [xml] $nuspec = $reader.ReadToEnd() }
    finally { $reader.Dispose() }
    Assert-Equal 'NuGet package ID' ([string] $nuspec.package.metadata.id) ([string] $manifest.package.id)
    Assert-Equal 'NuGet package version' ([string] $nuspec.package.metadata.version) $packageVersion
    Assert-Equal 'NuGet package license' ([string] $nuspec.package.metadata.license.'#text') `
        ([string] $manifest.package.licenseExpression)
}
finally {
    $zip.Dispose()
}

$nativeDirectory = Join-Path $PackageRoot 'runtimes\win-x64\native'
$runtimeInfo = @(& (Join-Path $PSScriptRoot 'Get-FfmpegBuildInfo.ps1') `
    -NativeDirectory $nativeDirectory)
$runtimeByFile = @{}
foreach ($item in $runtimeInfo) { $runtimeByFile[$item.File] = $item }

$expectedNames = @($manifest.libraries | ForEach-Object { [string] $_.file })
$actualNames = @(Get-ChildItem -LiteralPath $nativeDirectory -Filter '*.dll' -File |
    Select-Object -ExpandProperty Name)
$unexpectedNames = @($actualNames | Where-Object { $_ -notin $expectedNames })
if ($unexpectedNames.Count -ne 0) {
    throw "Unexpected native libraries in the runtime package: $($unexpectedNames -join ', ')"
}

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
    Package = "$($manifest.package.id) $packageVersion"
    Libraries = $manifest.libraries.Count
    FfmpegCommit = $manifest.nativeBuild.ffmpegCommit
    FlyleafCommit = $manifest.runtimeSource.releaseCommit
    Status = 'Verified'
}

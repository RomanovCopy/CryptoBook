[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$manifestPath = Join-Path $repositoryRoot 'compliance\ffmpeg\package-manifest.json'
$projectPath = Join-Path $PSScriptRoot 'CryptoBook.Flyleaf.FFmpeg.Runtime.Windows.X64.csproj'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'third_party\nuget'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$workDirectory = [IO.Path]::GetFullPath((Join-Path `
    $temporaryRoot `
    ('CryptoBook-Flyleaf-FFmpeg-' + [Guid]::NewGuid().ToString('N'))))
$temporaryPrefix = $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $workDirectory.StartsWith($temporaryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a work directory outside the system temporary directory: $workDirectory"
}

try {
    [IO.Directory]::CreateDirectory($workDirectory) | Out-Null
    $archivePath = Join-Path $workDirectory ([string] $manifest.runtimeSource.archiveName)
    Invoke-WebRequest `
        -UseBasicParsing `
        -Uri ([string] $manifest.runtimeSource.url) `
        -OutFile $archivePath

    $archive = Get-Item -LiteralPath $archivePath
    if ([long] $archive.Length -ne [long] $manifest.runtimeSource.size) {
        throw "Upstream archive size mismatch. Expected $($manifest.runtimeSource.size), got $($archive.Length)."
    }
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    if ($archiveHash -cne [string] $manifest.runtimeSource.sha256) {
        throw "Upstream archive SHA-256 mismatch. Expected $($manifest.runtimeSource.sha256), got $archiveHash."
    }

    $extractDirectory = Join-Path $workDirectory 'extracted'
    [IO.Directory]::CreateDirectory($extractDirectory) | Out-Null
    $sevenZip = Get-Command 7z.exe, 7z -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Source -First 1
    if ($sevenZip) {
        & $sevenZip x $archivePath "-o$extractDirectory" -y | Out-Null
    }
    else {
        $tar = Get-Command tar.exe, tar -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty Source -First 1
        if (-not $tar) {
            throw 'Neither 7-Zip nor bsdtar is available to extract the pinned Flyleaf archive.'
        }
        & $tar -xf $archivePath -C $extractDirectory
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Flyleaf archive extraction failed with exit code $LASTEXITCODE."
    }

    $nativeDirectory = Join-Path $extractDirectory 'FFmpeg'
    $expectedNames = @($manifest.libraries | ForEach-Object { [string] $_.file })
    $actualLibraries = @(Get-ChildItem -LiteralPath $nativeDirectory -Filter '*.dll' -File)
    $unexpectedNames = @($actualLibraries.Name | Where-Object { $_ -notin $expectedNames })
    if ($unexpectedNames.Count -ne 0) {
        throw "Unexpected native libraries in the Flyleaf archive: $($unexpectedNames -join ', ')"
    }

    foreach ($expected in $manifest.libraries) {
        $path = Join-Path $nativeDirectory ([string] $expected.file)
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Expected native library was not found: $path"
        }
        $file = Get-Item -LiteralPath $path
        if ([long] $file.Length -ne [long] $expected.size) {
            throw "$($expected.file) size mismatch."
        }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($hash -cne [string] $expected.sha256) {
            throw "$($expected.file) SHA-256 mismatch."
        }
    }

    & dotnet pack $projectPath `
        -c Release `
        -o $OutputDirectory `
        -p:FfmpegSourceDirectory=$nativeDirectory `
        -p:PackageVersion=$($manifest.package.version)
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime package creation failed with exit code $LASTEXITCODE."
    }

    $packagePath = Join-Path `
        $OutputDirectory `
        "$($manifest.package.id).$($manifest.package.version).nupkg"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Runtime package was not created: $packagePath"
    }

    Get-Item -LiteralPath $packagePath |
        Select-Object FullName, Length, @{ Name = 'SHA256'; Expression = {
            (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        } }
}
finally {
    if (Test-Path -LiteralPath $workDirectory) {
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
}

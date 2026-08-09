[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?([-.][0-9A-Za-z.-]+)?$')]
    [string] $Version = '1.1.1.0',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $OutputDirectory,

    [switch] $NoRestore,

    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'CryptoBook\CryptoBook.csproj'
$publishDirectory = Join-Path $repositoryRoot 'artifacts\win-x64'
$installerScript = Join-Path $PSScriptRoot 'CryptoBook.iss'
$minimumInstallerSdk = [Version]'8.0.423'

$dotnetCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
    (Get-Command dotnet.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -Unique

$dotnetPath = $null
foreach ($candidate in $dotnetCandidates) {
    $candidateVersionText = & $candidate --version 2>$null
    $candidateVersion = $null
    if ([Version]::TryParse($candidateVersionText, [ref]$candidateVersion) -and
        $candidateVersion -ge $minimumInstallerSdk) {
        $dotnetPath = $candidate
        break
    }
}

if (-not $dotnetPath) {
    throw "Building the installer requires .NET SDK $minimumInstallerSdk or newer in the 8.0 feature band."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts'
}

$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

if (-not $SkipPublish) {
    $artifactsDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
    $resolvedPublishDirectory = [IO.Path]::GetFullPath($publishDirectory)
    $expectedPrefix = $artifactsDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPublishDirectory.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a publish directory outside artifacts: $resolvedPublishDirectory"
    }

    if (-not $NoRestore) {
        & $dotnetPath restore $projectPath --locked-mode -r win-x64
        if ($LASTEXITCODE -ne 0) {
            throw 'Dependency restore failed.'
        }
    }

    if (Test-Path -LiteralPath $resolvedPublishDirectory) {
        Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
    }

    & $dotnetPath publish $projectPath `
        -c $Configuration `
        --no-restore `
        -r win-x64 `
        --self-contained true `
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:PublishTrimmed=false `
        -o $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Application publish failed.'
    }
}

$applicationPath = Join-Path $publishDirectory 'CryptoBook.exe'
if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
    throw "Published application not found: $applicationPath"
}

$unexpectedPublishFiles = @(
    Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
        Where-Object { $_.FullName -ne $applicationPath }
)
if ($unexpectedPublishFiles.Count -ne 0) {
    throw "Single-file publish contains unexpected files: $($unexpectedPublishFiles.FullName -join ', ')"
}

$compilerCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

$compilerPath = $compilerCandidates | Select-Object -First 1
if (-not $compilerPath) {
    throw 'Inno Setup 6 was not found. Install it from https://jrsoftware.org/isdl.php and run this script again.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$numericVersion = $Version.Split('-', 2)[0].Split('+', 2)[0]
$numericVersionParts = $numericVersion.Split('.')
$versionInfoVersion = if ($numericVersionParts.Count -eq 3) {
    "$numericVersion.0"
} else {
    $numericVersion
}

& $compilerPath `
    "/DSourceDir=$publishDirectory" `
    "/DOutputDir=$OutputDirectory" `
    "/DMyAppVersion=$Version" `
    "/DVersionInfoVersion=$versionInfoVersion" `
    $installerScript
if ($LASTEXITCODE -ne 0) {
    throw 'Installer compilation failed.'
}

$installerPath = Join-Path $OutputDirectory "CryptoBook-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer was not created: $installerPath"
}

Write-Output $installerPath

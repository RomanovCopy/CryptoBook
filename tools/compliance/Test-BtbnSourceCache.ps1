[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CacheDirectory,
    [string] $RecipePath = (Join-Path $PSScriptRoot `
        '..\..\compliance\ffmpeg\btbn-win64-gpl-shared-7.1.Dockerfile'),
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
$RecipePath = [IO.Path]::GetFullPath($RecipePath)
if (-not (Test-Path -LiteralPath $CacheDirectory -PathType Container)) {
    throw "BtbN source cache was not found: $CacheDirectory"
}

$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in Get-Content -LiteralPath $RecipePath) {
    foreach ($match in [regex]::Matches($line, '\.cache/downloads/(?<file>[^,]+\.tar\.xz)')) {
        [void] $expected.Add($match.Groups['file'].Value)
    }
}

$missing = @($expected | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $CacheDirectory $_) -PathType Leaf)
} | Sort-Object)
if ($missing.Count -gt 0) {
    throw "Missing $($missing.Count) required source archives:`n$($missing -join "`n")"
}

$records = foreach ($name in $expected | Sort-Object) {
    $path = Join-Path $CacheDirectory $name
    $file = Get-Item -LiteralPath $path
    [ordered]@{
        file = $name
        size = $file.Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}

$result = [ordered]@{
    schemaVersion = 1
    recipeSha256 = (Get-FileHash -LiteralPath $RecipePath -Algorithm SHA256).Hash
    requiredArchiveCount = $expected.Count
    archives = @($records)
}
if ($OutputPath) {
    $OutputPath = [IO.Path]::GetFullPath($OutputPath)
    [IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
    [IO.File]::WriteAllText(
        $OutputPath,
        (($result | ConvertTo-Json -Depth 5) + "`n"),
        [Text.UTF8Encoding]::new($false))
}

[pscustomobject]@{
    RequiredArchives = $expected.Count
    TotalBytes = ($records | Measure-Object -Property size -Sum).Sum
    Status = 'Complete'
    Manifest = $OutputPath
}

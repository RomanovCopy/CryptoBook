[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $PSScriptRoot '..\..\artifacts\ffmpeg-core-source')
)

$ErrorActionPreference = 'Stop'
$Destination = [IO.Path]::GetFullPath($Destination)
[IO.Directory]::CreateDirectory($Destination) | Out-Null

$sources = @(
    [ordered]@{
        Name = 'ffmpeg'
        Repository = 'https://github.com/FFmpeg/FFmpeg.git'
        Commit = '10aaf84f855dbcedb8ee2e3fce307e9b98320946'
        Archive = 'ffmpeg-10aaf84f855dbcedb8ee2e3fce307e9b98320946.zip'
    },
    [ordered]@{
        Name = 'btbn-ffmpeg-builds'
        Repository = 'https://github.com/BtbN/FFmpeg-Builds.git'
        Commit = 'dc38e41621fd62eec41a467dad15462efdb0d516'
        Archive = 'btbn-ffmpeg-builds-dc38e41621fd62eec41a467dad15462efdb0d516.zip'
    }
)

$records = [Collections.Generic.List[object]]::new()
foreach ($source in $sources) {
    $work = [IO.Path]::GetFullPath((Join-Path $Destination ".work-$($source.Name)"))
    $destinationPrefix = $Destination.TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $work.StartsWith($destinationPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use a work directory outside the destination: $work"
    }
    if (Test-Path -LiteralPath $work) {
        throw "Temporary clone already exists: $work"
    }

    try {
        & git clone --quiet --filter=blob:none --no-checkout $source.Repository $work
        if ($LASTEXITCODE -ne 0) { throw "Clone failed: $($source.Repository)" }
        & git -C $work fetch --quiet --depth=1 origin $source.Commit
        if ($LASTEXITCODE -ne 0) { throw "Fetch failed: $($source.Commit)" }
        $resolved = (& git -C $work rev-parse $source.Commit).Trim()
        if ($resolved -cne $source.Commit) {
            throw "Commit mismatch for $($source.Name): $resolved"
        }

        $archivePath = Join-Path $Destination $source.Archive
        & git -C $work archive --format=zip `
            --prefix="$($source.Name)-$($source.Commit)/" `
            --output=$archivePath $source.Commit
        if ($LASTEXITCODE -ne 0) { throw "Archive failed: $($source.Name)" }

        $file = Get-Item -LiteralPath $archivePath
        $records.Add([ordered]@{
            name = $source.Name
            repository = $source.Repository
            commit = $source.Commit
            file = $file.Name
            size = $file.Length
            sha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
        })
    }
    finally {
        if (Test-Path -LiteralPath $work) {
            $resolvedWork = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $work).Path)
            if (-not $resolvedWork.StartsWith($destinationPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove a work directory outside the destination: $resolvedWork"
            }
            Remove-Item -LiteralPath $resolvedWork -Recurse -Force
        }
    }
}

$record = [ordered]@{
    schemaVersion = 1
    scope = 'Exact FFmpeg core source and BtbN build recipe; not the complete source set for linked libraries.'
    artifacts = $records
}
$recordPath = Join-Path $Destination 'SHA256SUMS.json'
[IO.File]::WriteAllText(
    $recordPath,
    (($record | ConvertTo-Json -Depth 6) + "`n"),
    [Text.UTF8Encoding]::new($false))

Get-ChildItem -LiteralPath $Destination | Select-Object Name,Length

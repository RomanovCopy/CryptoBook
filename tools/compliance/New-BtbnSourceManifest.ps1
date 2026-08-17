[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $RecipeRoot,
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\..\compliance\ffmpeg\source-pins.json'),
    [string] $RecipeCommit = 'dc38e41621fd62eec41a467dad15462efdb0d516'
)

$ErrorActionPreference = 'Stop'
$RecipeRoot = [IO.Path]::GetFullPath($RecipeRoot)
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$dockerfile = Join-Path $RecipeRoot 'Dockerfile'
if (-not (Test-Path -LiteralPath $dockerfile -PathType Leaf)) {
    throw "Generated BtbN Dockerfile was not found: $dockerfile"
}

$actualCommit = (& git -C $RecipeRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to read the BtbN checkout commit.' }
if ($actualCommit -cne $RecipeCommit) {
    throw "Recipe checkout mismatch. Expected $RecipeCommit, got $actualCommit."
}

$stagePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in Get-Content -LiteralPath $dockerfile) {
    if ($line -match '^ENV SELF="(?<path>scripts\.d/.+\.sh)"') {
        [void] $stagePaths.Add($Matches.path)
    }
}

$pins = [Collections.Generic.List[object]]::new()
foreach ($stagePath in $stagePaths | Sort-Object) {
    $nativePath = Join-Path $RecipeRoot ($stagePath -replace '/', [IO.Path]::DirectorySeparatorChar)
    $variables = @{}
    foreach ($line in Get-Content -LiteralPath $nativePath) {
        if ($line -match '^\s*(?<name>SCRIPT_(?:REPO|COMMIT|REV|BRANCH)\d*)="(?<value>[^"]+)"') {
            $variables[$Matches.name] = $Matches.value
        }
    }

    foreach ($name in $variables.Keys | Where-Object { $_ -match '^SCRIPT_REPO(?<suffix>\d*)$' } | Sort-Object) {
        $suffix = if ($name -match '^SCRIPT_REPO(?<suffix>\d*)$') { $Matches.suffix } else { '' }
        $revisionName = if ($variables.ContainsKey("SCRIPT_COMMIT$suffix")) {
            "SCRIPT_COMMIT$suffix"
        }
        elseif ($variables.ContainsKey("SCRIPT_REV$suffix")) {
            "SCRIPT_REV$suffix"
        }
        else {
            $null
        }

        $pins.Add([ordered]@{
            stage = $stagePath
            repository = $variables[$name]
            revision = if ($revisionName) { $variables[$revisionName] } else { $null }
            revisionKind = if ($revisionName -like 'SCRIPT_REV*') { 'svn-revision' } else { 'git-ref' }
            branch = if ($variables.ContainsKey("SCRIPT_BRANCH$suffix")) {
                $variables["SCRIPT_BRANCH$suffix"]
            } else { $null }
            scriptSha256 = (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash
        })
    }
}

$output = [ordered]@{
    schemaVersion = 1
    recipe = [ordered]@{
        repository = 'https://github.com/BtbN/FFmpeg-Builds.git'
        commit = $RecipeCommit
        command = './generate.sh win64 gpl-shared 7.1'
        variant = 'win64-gpl-shared'
        releaseLine = '7.1'
        generatedDockerfileSha256 = (Get-FileHash -LiteralPath $dockerfile -Algorithm SHA256).Hash
    }
    ffmpeg = [ordered]@{
        repository = 'https://github.com/FFmpeg/FFmpeg.git'
        commit = '10aaf84f855dbcedb8ee2e3fce307e9b98320946'
    }
    enabledStageCount = $stagePaths.Count
    declaredSourcePinCount = $pins.Count
    declaredSourcePins = $pins
    scopeNote = 'Pins statically declared by enabled BtbN stages. Preserve the entire pinned recipe because custom download functions and patches are authoritative.'
}

$parent = Split-Path -Parent $OutputPath
[IO.Directory]::CreateDirectory($parent) | Out-Null
$json = $output | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($OutputPath, "$json`n", [Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $OutputPath

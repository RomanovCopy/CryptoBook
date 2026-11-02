[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string[]]$Cultures = @('en-US', 'ru-RU', 'uk-UA', 'de-DE')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$ExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Use a new output directory to preserve earlier evidence: $OutputDirectory"
}
if (@($Cultures | Where-Object { $_ -notin @('en-US', 'ru-RU', 'uk-UA', 'de-DE') }).Count) {
    throw 'Unsupported culture.'
}
$hostDirectory = Join-Path $repositoryRoot 'tools/HelpValidation/bin/Release/net10.0-windows10.0.17763.0/win-x64'
$hook = Join-Path $hostDirectory 'HelpValidation.dll'
if (-not (Test-Path -LiteralPath $hook -PathType Leaf)) { throw 'Build HelpValidation in Release first.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$toolDirectory = Join-Path $OutputDirectory 'tool'
New-Item -ItemType Directory -Path $toolDirectory | Out-Null
Copy-Item -LiteralPath $hook -Destination $toolDirectory
$helpRoot = Join-Path $repositoryRoot 'docs/help/content'
$manifest = [ordered]@{}
Get-ChildItem -LiteralPath $helpRoot -File -Recurse |
    Where-Object Extension -in @('.json', '.md', '.png') | Sort-Object FullName | ForEach-Object {
        $manifest[[IO.Path]::GetRelativePath($helpRoot, $_.FullName).Replace('\', '/')] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'expected-help.json') -Encoding utf8NoBOM
$commit = (git -C $repositoryRoot rev-parse HEAD).Trim() + '+stage12-working-tree'
$summaries = @()
foreach ($culture in $Cultures) {
    $run = Join-Path $OutputDirectory $culture
    New-Item -ItemType Directory -Path $run,(Join-Path $run 'tmp'),(Join-Path $run 'bundle') | Out-Null
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'expected-help.json') -Destination $run
    $configuration = Join-Path $run 'check.json'
    @($run, $culture, $commit, 'stage12-package') | ConvertTo-Json |
        Set-Content -LiteralPath $configuration -Encoding utf8NoBOM
    $start = [Diagnostics.ProcessStartInfo]::new($ExecutablePath)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $run
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['DOTNET_STARTUP_HOOKS'] = Join-Path $toolDirectory 'HelpValidation.dll'
    $start.Environment['CRYPTOBOOK_HELP_PACKAGE_CHECK'] = $configuration
    $start.Environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $run 'bundle'
    $start.Environment['TEMP'] = Join-Path $run 'tmp'
    $start.Environment['TMP'] = Join-Path $run 'tmp'
    $start.Environment.Remove('CRYPTOBOOK_HELP_STAGE11_EVIDENCE_TAG') | Out-Null
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(240000)) {
        $process.Kill($true)
        throw "Package check timed out: $culture"
    }
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $run 'stdout.log') -Encoding utf8NoBOM
    $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $run 'stderr.log') -Encoding utf8NoBOM
    if ($process.ExitCode -ne 0) {
        Get-Content -LiteralPath (Join-Path $run 'stderr.log')
        throw "Package check failed: $culture (exit $($process.ExitCode))."
    }
    $result = @(Get-ChildItem -LiteralPath (Join-Path $run "artifacts/hv/$culture") -Filter result.json -Recurse)
    if ($result.Count -ne 1) { throw "Expected exactly one result for $culture." }
    $summary = Get-Content -LiteralPath $result[0].FullName -Raw | ConvertFrom-Json
    if (-not $summary.stage12 -or $summary.bindingErrors.Count -ne 0) { throw "Invalid package result: $culture" }
    $summaries += [pscustomobject]@{ culture = $culture; checks = $summary.checks; result = $result[0].FullName }
    Write-Output "HELP_PACKAGE PASS culture=$culture checks=$($summary.checks)"
    $process.Dispose()
}
[ordered]@{
    executable = $ExecutablePath
    executableSha256 = (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash
    hookSha256 = (Get-FileHash -LiteralPath (Join-Path $toolDirectory 'HelpValidation.dll') -Algorithm SHA256).Hash
    expectedHelpFiles = $manifest.Count
    runs = $summaries
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding utf8NoBOM

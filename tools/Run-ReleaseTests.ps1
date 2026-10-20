[CmdletBinding()]
param([string] $ResultsDirectory = 'artifacts/TestResults')

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'CryptoBook.Tests/CryptoBook.Tests.csproj'
if (-not [IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repositoryRoot $ResultsDirectory
}
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release test build failed.' }

# Other WPF tests create Application.Current on their own STA thread. Keep AI
# command notification tests in a fresh process so they cannot invoke that stale
# dispatcher synchronously. Include/exclude the same class to run every test once.
$groups = @(
    @{ Name = 'ai-integration'; Filter = '-class' },
    @{ Name = 'remaining'; Filter = '-class-' }
)
$total = 0
foreach ($group in $groups) {
    $report = Join-Path $ResultsDirectory ($group.Name + '.trx')
    dotnet run --project $project -c Release --no-build --no-restore -- `
        $group.Filter 'CryptoBook.Tests.AiIntegrationTests' `
        -longRunning 30 -reporter verbose -result-trx $report
    if ($LASTEXITCODE -ne 0) { throw "Release tests failed: $($group.Name)." }
    [xml]$results = Get-Content -LiteralPath $report
    $counters = $results.TestRun.ResultSummary.Counters
    if ([int]$counters.total -lt 1 -or [int]$counters.passed -ne [int]$counters.total) {
        throw "Expected passing tests without skips: $($group.Name)."
    }
    $total += [int]$counters.total
}
Write-Output "RELEASE_TESTS: PASS ($total tests across $($groups.Count) isolated processes)"

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
# command notification, runtime monitor and document search XAML tests in fresh
# processes so they cannot invoke that stale dispatcher synchronously. Exclude them from
# the remaining group to run every test once.
# Worker cancellation tests also need a fresh process: loading cancellation can
# hang in the combined WPF group while the isolated cancellation group passes.
# The visible batch rename window needs its own Application dispatcher as well.
$groups = @(
    @{ Name = 'ai-integration'; Arguments = @('-class', 'CryptoBook.Tests.AiIntegrationTests') },
    @{ Name = 'ai-runtime'; Arguments = @('-class', 'CryptoBook.Tests.AdaptiveAiInferenceTests') },
    @{ Name = 'document-search'; Arguments = @('-class', 'CryptoBook.Tests.DocumentSearchTests') },
    @{ Name = 'worker-cancellation'; Arguments = @('-class', 'CryptoBook.Tests.AiWorkerCancellationTests') },
    @{ Name = 'batch-rename-window'; Arguments = @('-class', 'CryptoBook.Tests.BatchRenameWindowTests') },
    @{ Name = 'remaining'; Arguments = @('-class-', 'CryptoBook.Tests.AiIntegrationTests', '-class-', 'CryptoBook.Tests.AdaptiveAiInferenceTests', '-class-', 'CryptoBook.Tests.DocumentSearchTests', '-class-', 'CryptoBook.Tests.AiWorkerCancellationTests', '-class-', 'CryptoBook.Tests.BatchRenameWindowTests') }
)
$total = 0
foreach ($group in $groups) {
    $report = Join-Path $ResultsDirectory ($group.Name + '.trx')
    $filterArguments = $group.Arguments
    dotnet run --project $project -c Release --no-build --no-restore -- `
        @filterArguments `
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

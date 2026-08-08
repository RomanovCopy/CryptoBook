param(
    [ValidateSet('all', 'launch', 'catalog', 'search', 'images', 'encryption')]
    [string]$Scenario = 'all',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solution = Join-Path $repositoryRoot 'CryptoBook\CryptoBook.sln'
$project = Join-Path $repositoryRoot 'CryptoBook.Performance\CryptoBook.Performance.csproj'

dotnet restore $solution --locked-mode
dotnet build $solution -c $Configuration --no-restore
dotnet run --project $project -c $Configuration --no-build -- $Scenario

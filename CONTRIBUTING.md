# Contributing to CryptoBook

Thanks for considering a contribution.

## Development environment

CryptoBook targets Windows and .NET 10. Use SDK 10.0.400 or newer in the 10.0
feature band. Visual Studio users should install the
**.NET desktop development** workload.

Clone the repository and restore locked dependencies:

```powershell
git clone https://github.com/RomanovCopy/CryptoBook.git
cd CryptoBook
dotnet restore CryptoBook/CryptoBook.sln --locked-mode
```

## Branch workflow

`master` is the stable integration branch. Ongoing project updates are developed on
`updates` and merged into `master` through pull requests.

For a normal project update:

```powershell
git switch updates
git pull --ff-only origin updates
# make and commit the change
git push origin updates
```

Then open a pull request from `updates` into `master`.

For isolated external contributions, fixes or automated changes, use a focused branch and
target `master` with the pull request.

CI runs on pushes to `updates` and `master`, and on pull requests targeting `master`.

## Build and test

Before opening a pull request, run:

```powershell
dotnet build CryptoBook/CryptoBook.sln -c Release --no-restore
dotnet test CryptoBook/CryptoBook.sln -c Release --no-restore
```

The project uses xUnit and STA tests for WPF. Compiler warnings and detected NuGet
vulnerabilities are treated as errors in the release workflow.

## Pull requests

- keep each pull request limited to one coherent change;
- add or update tests when behavior changes;
- describe behavior before and after the change;
- mention user-visible changes and compatibility implications;
- avoid unrelated formatting or refactoring in the same pull request.

## Architecture

CryptoBook is a WPF application built around MVVM and dependency injection with Autofac.
Prefer changes that preserve separation between views, view models and services. Avoid moving
application logic into code-behind when a command, behavior or service is the appropriate layer.

## Security-sensitive changes

Changes involving cryptography, protected file formats, key lifetime, recovery, updater logic,
release automation or dependency provenance require additional review. Do not weaken validation,
authentication, atomic-write guarantees or recovery checks for convenience.

Potential vulnerabilities should be reported according to [SECURITY.md](SECURITY.md), not through
a public issue.

## Releases

Production release requirements, artifact integrity and recovery procedures are documented in
[docs/PRODUCTION.md](docs/PRODUCTION.md).

## Licensing

By contributing, you agree that your contribution is distributed under the repository's
`GPL-3.0-only` license and that you have the right to submit the contributed material.

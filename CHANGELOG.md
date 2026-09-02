# Changelog

## 1.1.2.5 - 2026-09-02

### Highlights

- Migrated the application, tests, build scripts, and release workflows from
  .NET 8 to .NET 10. Official self-contained builds now include the pinned
  .NET 10.0.11 runtime.
- Updated the media stack to Flyleaf 3.11.3 and FFmpeg 9. The exact native
  runtime is now kept in a repository-local NuGet package so locked restores
  and release builds use the verified binaries.
- Hardened media startup by checking the complete required FFmpeg DLL set and
  added a release smoke test that builds and validates the Flyleaf WPF control
  template in clean CI environments.
- Updated Autofac, Microsoft.Data.Sqlite, SQLitePCLRaw, Material Design, WPF
  Behaviors, test infrastructure, and their locked dependency graphs.
- Expanded FFmpeg/Flyleaf provenance checks and release evidence, refreshed
  third-party notices, and included the local runtime package and reconstruction
  tooling in the provenance archive.
- Modernized the legacy PBKDF2 implementation without changing its derivation
  parameters, and cleaned up obsolete or ambiguous framework references after
  the platform migration.

Full comparison: [v1.1.2.4...v1.1.2.5](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.2.4...v1.1.2.5)

Older releases are available on the
[GitHub Releases page](https://github.com/RomanovCopy/CryptoBook/releases).

# Changelog

## 1.1.2.51 - 2026-09-02

### Fixed

- Fixed an in-place upgrade failure where the new .NET 10 single-file
  executable could load `System.Private.CoreLib.dll` and other runtime files
  left by an earlier .NET 8 multi-file installation, then exit before opening
  the main window.
- The installer now removes only known legacy runtime files and directories
  before copying the current single-file application. It deliberately avoids a
  broad deletion of the installation directory.
- Added regression coverage for the legacy-runtime cleanup rules.
- Because affected 1.1.2.5 installations fail before application startup, they
  cannot use the built-in updater. Version 1.1.2.51 must be installed manually
  once over the existing installation; user documents and settings are kept.

Full comparison: [v1.1.2.5...v1.1.2.51](https://github.com/RomanovCopy/CryptoBook/compare/v1.1.2.5...v1.1.2.51)

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

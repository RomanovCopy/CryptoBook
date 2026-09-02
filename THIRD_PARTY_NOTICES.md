# Third-party notices

CryptoBook uses the third-party components listed below. This notice is based
on the dependency locks committed for CryptoBook 1.1.2.51 and was audited on
2026-09-02. A package's own license and notices control if this summary differs
from them. CryptoBook does not claim ownership of third-party software.

The GNU GPL v3 text is in `LICENSE`. Other standard license texts are in
`LICENSES/`. Source availability is described in `SOURCE_CODE.md`.

## Components included in application distributions

### GPL-3.0-or-later

- `CryptoBook.Flyleaf.FFmpeg.Runtime.Windows.X64` 9.0.20260816 — CryptoBook
  repackaging of the unmodified native libraries published by SuRGeoNix in the
  official Flyleaf v3.11.3 release.
- FFmpeg native libraries — FFmpeg developers.
  Project: <https://ffmpeg.org/>. The libraries identify exact commit
  `0056dd32fd94e739e14bb3c463c68ebe806dfd1d`, report
  `GPL version 3 or later`, and were built with `--enable-gpl --enable-version3`
  plus the configuration recorded in `compliance/ffmpeg/package-manifest.json`.
  Exact archive, package and DLL hashes are recorded in `compliance/ffmpeg/`.

License text: `LICENSE`.

### LGPL-3.0-or-later

- `FlyleafLib` 3.11.3 — SuRGeoNix, © 2026.
- `FlyleafLib.Controls.WPF` 1.7.3 — SuRGeoNix, © 2026.
- `Flyleaf.FFmpeg.Bindings` 9.0.0 — SuRGeoNix, © 2026.

Project: <https://github.com/SuRGeoNix/Flyleaf>. License supplement:
`LICENSES/LGPL-3.0-or-later.txt`; the GNU GPL v3 text it incorporates is in
`LICENSE`.

### Apache-2.0

- `SQLitePCLRaw.bundle_e_sqlite3`, `SQLitePCLRaw.config.e_sqlite3`,
  `SQLitePCLRaw.core`, and `SQLitePCLRaw.provider.e_sqlite3` 3.0.5 — Copyright
  2014–2026 SourceGear, LLC.

Project: <https://github.com/ericsink/SQLitePCL.raw>. License text:
`LICENSES/Apache-2.0.txt`. The `SQLite` 3.53.4 package contains the native
SQLite library, which is dedicated to the public domain by the SQLite project;
see <https://www.sqlite.org/copyright.html>.

### MIT

- Self-contained Microsoft .NET Runtime and Windows Desktop Runtime 10.0.11 —
  Copyright © .NET Foundation and contributors. Exact runtime license and
  bundled notices: `LICENSES/DOTNET-10.0.11-LICENSE.txt` and
  `LICENSES/DOTNET-10.0.11-THIRD-PARTY-NOTICES.txt`.
- `Autofac` 9.3.2 and `Autofac.Extensions.DependencyInjection` 11.0.2 —
  Copyright © Autofac Contributors.
- `Konscious.Security.Cryptography.Argon2` 1.3.1 and
  `Konscious.Security.Cryptography.Blake2` 1.1.1 — Copyright © Keef Aragon.
- `MaterialDesignThemes` and `MaterialDesignColors` 5.3.3-ci1443 — Copyright
  2025 James Willock/Mulholland Software Ltd.
- `Dragablz` 0.0.3.234 — Copyright James Willock, Mulholland Software and
  contributors.
- `Microsoft.Data.Sqlite` and `Microsoft.Data.Sqlite.Core` 10.0.11;
  `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.9;
  `Microsoft.Xaml.Behaviors.Wpf` 1.1.158; `System.Management` 10.0.11 —
  Microsoft and .NET Foundation contributors.
- `Mono.Posix.NETStandard` 5.20.1-preview — Mono contributors/Microsoft.
- `SharpGen.Runtime` and `SharpGen.Runtime.COM` 2.4.2-beta — Copyright
  2010–2017 Alexandre Mutel, 2017–2023 Jeremy Koritzinsky, and 2023–2024
  Amer Koleci.
- `Vortice.D3DCompiler`, `Vortice.Direct2D1`, `Vortice.Direct3D11`,
  `Vortice.DirectComposition`, `Vortice.DirectX`, `Vortice.DXGI`,
  `Vortice.MediaFoundation`, and `Vortice.XAudio2` 3.8.3, plus
  `Vortice.Mathematics` 2.1.1 — Copyright Amer Koleci and contributors.
- `WpfColorFontDialog` 1.0.9 — Copyright © 2015 Sverre Kristoffer Skodje.

License text: `LICENSES/MIT.txt`.

`Microsoft.NET.ILLink.Tasks` 10.0.11 is MIT-licensed build tooling marked as a
private asset; it is not intended to be included in the application runtime.

## Development and test-only components

The source tree also locks the following packages used for tests or tooling:

- MIT: `Microsoft.NET.Test.Sdk`, `Microsoft.TestPlatform.ObjectModel`,
  `Microsoft.TestPlatform.TestHost`, and `Microsoft.CodeCoverage` 18.9.0;
  `Microsoft.Testing.Platform`, `Microsoft.Testing.Platform.MSBuild`,
  `Microsoft.Testing.Extensions.Telemetry`, and
  `Microsoft.Testing.Extensions.TrxReport.Abstractions` 2.3.3.
- Apache-2.0: the `xunit.v3` framework and runner packages 4.0.0;
  `xunit.analyzers` 2.0.0; `xunit.runner.visualstudio` 4.0.0.
- MS-PL: `Xunit.StaFact` 4.0.23.

Corresponding standard texts are in `LICENSES/`.

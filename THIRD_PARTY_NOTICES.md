# Third-party notices

CryptoBook uses the third-party components listed below. This notice is based
on the dependency locks committed for CryptoBook 1.1.2.1 and was audited on
2026-08-17. A package's own license and notices control if this summary differs
from them. CryptoBook does not claim ownership of third-party software.

The GNU GPL v3 text is in `LICENSE`. Other standard license texts are in
`LICENSES/`. Source availability is described in `SOURCE_CODE.md`.

## Components included in application distributions

### GPL-3.0-only

- `Sdcb.FFmpeg.runtime.windows-x64` 7.1.0 — sdcb, Copyright 2024.
  The package contains native FFmpeg libraries and declares `GPL-3.0-only`.
  The package metadata's old URL redirects to
  <https://github.com/sdcb/Sdcb.FFmpeg>. Exact package/DLL hashes and provenance
  are recorded in `compliance/ffmpeg/`.
- FFmpeg native libraries — FFmpeg developers.
  Project: <https://ffmpeg.org/>. The libraries identify exact commit
  `10aaf84f855dbcedb8ee2e3fce307e9b98320946`, report
  `GPL version 3 or later`, and were built with `--enable-gpl --enable-version3`
  plus the dependency set recorded in `compliance/ffmpeg/package-manifest.json`.

License text: `LICENSE`.

### LGPL-3.0-or-later

- `FlyleafLib` 3.10.4 — SuRGeoNix, © 2026.
- `FlyleafLib.Controls.WPF` 1.6.4 — SuRGeoNix, © 2026.
- `Flyleaf.FFmpeg.Bindings` 7.1.1 — SuRGeoNix, © 2025.

Project: <https://github.com/SuRGeoNix/Flyleaf>. License supplement:
`LICENSES/LGPL-3.0-or-later.txt`; the GNU GPL v3 text it incorporates is in
`LICENSE`.

### Apache-2.0

- `SQLitePCLRaw.bundle_e_sqlite3`, `SQLitePCLRaw.core`,
  `SQLitePCLRaw.lib.e_sqlite3`, and `SQLitePCLRaw.provider.e_sqlite3` 2.1.12 —
  Copyright 2014–2024 SourceGear, LLC.

Project: <https://github.com/ericsink/SQLitePCL.raw>. License text:
`LICENSES/Apache-2.0.txt`. The SQLite library itself is dedicated to the public
domain by the SQLite project; see <https://www.sqlite.org/copyright.html>.

### MIT

- Self-contained Microsoft .NET Runtime and Windows Desktop Runtime 8.0.29 —
  Copyright © .NET Foundation and contributors. Exact runtime license and
  bundled notices: `LICENSES/DOTNET-8.0.29-LICENSE.txt` and
  `LICENSES/DOTNET-8.0.29-THIRD-PARTY-NOTICES.txt`.
- `Autofac` 9.0.0 and `Autofac.Extensions.DependencyInjection` 10.0.0 —
  Copyright © Autofac Contributors.
- `Konscious.Security.Cryptography.Argon2` 1.3.1 and
  `Konscious.Security.Cryptography.Blake2` 1.1.1 — Copyright © Keef Aragon.
- `MaterialDesignThemes` and `MaterialDesignColors` 5.3.1-ci1190 — Copyright
  2025 James Willock/Mulholland Software Ltd.
- `Dragablz` 0.0.3.234 — Copyright James Willock, Mulholland Software and
  contributors.
- `Microsoft.Data.Sqlite` and `Microsoft.Data.Sqlite.Core` 8.0.28;
  `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.1;
  `Microsoft.Xaml.Behaviors.Wpf` 1.1.135; `System.CodeDom` 10.0.1;
  `System.Diagnostics.DiagnosticSource` 10.0.0; `System.IO.Pipelines` 9.0.1;
  `System.Management` 10.0.1; `System.Memory` 4.5.4;
  `System.Text.Encodings.Web` and `System.Text.Json` 9.0.1 — Microsoft and
  .NET Foundation contributors.
- `Mono.Posix.NETStandard` 5.20.1-preview — Mono contributors/Microsoft.
- `SharpGen.Runtime` and `SharpGen.Runtime.COM` 2.4.2-beta — Copyright
  2010–2017 Alexandre Mutel, 2017–2023 Jeremy Koritzinsky, and 2023–2024
  Amer Koleci.
- `Vortice.D3DCompiler`, `Vortice.Direct2D1`, `Vortice.Direct3D11`,
  `Vortice.DirectComposition`, `Vortice.DirectX`, `Vortice.DXGI`,
  `Vortice.MediaFoundation`, and `Vortice.XAudio2` 3.7.6-beta, plus
  `Vortice.Mathematics` 1.9.3 — Copyright Amer Koleci and contributors.
- `WpfColorFontDialog` 1.0.8 — Copyright © 2015 Sverre Kristoffer Skodje.

License text: `LICENSES/MIT.txt`.

`Microsoft.NET.ILLink.Tasks` 8.0.29 is MIT-licensed build tooling marked as a
private asset; it is not intended to be included in the application runtime.

## Development and test-only components

The source tree also locks the following packages used for tests or tooling:

- MIT: `Microsoft.NET.Test.Sdk`, `Microsoft.TestPlatform.ObjectModel`,
  `Microsoft.TestPlatform.TestHost`, and `Microsoft.CodeCoverage` 17.14.1;
  `Newtonsoft.Json` 13.0.3; `System.Collections.Immutable` and
  `System.Reflection.Metadata` 8.0.0.
- Apache-2.0: `xunit`, `xunit.assert`, `xunit.core`,
  `xunit.extensibility.core`, and `xunit.extensibility.execution` 2.9.3;
  `xunit.analyzers` 1.18.0; `xunit.runner.visualstudio` 3.1.1. The legacy
  `xunit.abstractions` 2.0.3 package also points to the xUnit license notice.
- MS-PL: `Xunit.StaFact` 1.1.11.

Corresponding standard texts are in `LICENSES/`.

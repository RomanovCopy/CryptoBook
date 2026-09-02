# Licensing audit

Audit date: 2026-09-02
Audited revision: working tree based on commit `a30c0a0`
Scope: tracked source, project files, dependency locks, NuGet metadata,
application resources, installer, and release workflow.

This is an engineering compliance review, not legal advice.

## Recommendation

Use `GPL-3.0-only` for CryptoBook while the distributed application contains
the FFmpeg 9 libraries repackaged as
`CryptoBook.Flyleaf.FFmpeg.Runtime.Windows.X64` 9.0.20260816. Those libraries
report `GPL version 3 or later`, while Flyleaf components declare
`LGPL-3.0-or-later`. GPL-3.0-only is compatible with the remaining MIT,
Apache-2.0, LGPL-3.0-or-later, and public-domain runtime components in the
current dependency graph.

If the desired project license is MIT, Apache-2.0, or proprietary, first remove
the GPL runtime and use a verified LGPL-compatible FFmpeg build. Then repeat
the dependency and binary audit; changing only the root `LICENSE` is not
sufficient.

## Findings

| Severity | Finding | Recommended action |
| --- | --- | --- |
| High | No root license or third-party notice existed, although release binaries embed third-party managed and native components. | Add and ship the prepared license set with every installer and ZIP. |
| High | The distributed FFmpeg runtime is declared `GPL-3.0-only`; the project previously had no GPL-compatible grant. | License project-authored code as GPL-3.0-only while this runtime remains. |
| High (partially resolved) | The official Flyleaf runtime contains GPL FFmpeg DLLs without complete Corresponding Source. The exact official archive, package and DLL hashes, FFmpeg core commit, Flyleaf release commit and embedded configuration are fixed and verified. The custom build recipe, .NET patch set and exact linked-dependency source revisions are not published. | Use the checked-in verifier and source snapshot, obtain the missing build/source material from Flyleaf, then retain the complete source and patch set beside the binary release. |
| High | The release workflow packaged only `CryptoBook.exe`; the installer and ZIP did not include license texts or notices. | Update packaging so the installer and ZIP contain the license bundle. |
| Medium | A self-contained publish embeds .NET 10 runtime packs that do not appear in the project NuGet lock table. | Ship the exact .NET 10.0.11 license and runtime third-party notice captured from the pinned runtime pack. |
| Medium (partially resolved) | All 48 images now have a hash/metadata/Git inventory and contact sheet. Marketing composites rebuild byte-for-byte and the generated background has a prompt; the application icon and 37 legacy toolbar images still lack creator/license evidence. | Complete `compliance/assets/ATTESTATION.md` with supporting records or replace the unresolved icon families. |
| Resolved | The human contributor appeared in Git history under several display names but the same email. | On 2026-08-17 the project owner confirmed Романов Сергей as developer and copyright holder; this is now recorded in `COPYRIGHT.md` and build metadata. |
| Medium | Several runtime dependencies are prerelease versions (`MaterialDesign*`, `Mono.Posix`, `SharpGen`). | Prefer stable releases when compatible versions exist and repeat license review on every dependency update. |
| Low | No SPDX header policy exists for source files. | Add `SPDX-License-Identifier: GPL-3.0-only` to project-authored source over time and require it for new files. |
| Positive | NuGet lock files, deterministic builds, NuGet audit, SPDX SBOM generation, checksums, and tagged release builds are already configured. | Keep these controls and add a license-compliance check to CI. |

## Dependency summary

Application/runtime lock:

| License | Packages |
| --- | --- |
| GPL-3.0-or-later | `CryptoBook.Flyleaf.FFmpeg.Runtime.Windows.X64` 9.0.20260816 |
| LGPL-3.0-or-later | `FlyleafLib` 3.11.3; `FlyleafLib.Controls.WPF` 1.7.3; `Flyleaf.FFmpeg.Bindings` 9.0.0 |
| Apache-2.0 | `SQLitePCLRaw.bundle_e_sqlite3`, `.config.e_sqlite3`, `.core`, `.provider.e_sqlite3` 3.0.5 |
| MIT | Autofac family; Konscious Argon2/Blake2; MaterialDesign/Dragablz; Microsoft and System libraries; Mono.Posix; SharpGen; Vortice; WpfColorFontDialog |
| Public domain | `SQLite` 3.53.4 native code embedded by the e_sqlite3 bundle |
| MIT and bundled notices | Microsoft .NET/Windows Desktop Runtime 10.0.11 included by self-contained publish |

Test-only additions are MIT, Apache-2.0, and MS-PL as enumerated in
`THIRD_PARTY_NOTICES.md`. Package names, versions, and direct/transitive status
remain authoritative in the committed lock files.

## Prepared license set

- `LICENSE` — GNU GPL version 3 text and project license;
- `COPYRIGHT.md` — project copyright statement;
- `THIRD_PARTY_NOTICES.md` — component versions, attributions, and licenses;
- `SOURCE_CODE.md` — corresponding-source location and unresolved FFmpeg duty;
- `ASSET_PROVENANCE.md` — asset rights register and outstanding verification;
- `compliance/ffmpeg/` — native binary manifest, exact source/release mapping,
  package reproduction instructions and upstream evidence request;
- `compliance/assets/` — image manifest, contact sheet and attestation draft;
- `LICENSES/` — LGPL-3.0-or-later, MIT, Apache-2.0, and MS-PL texts plus
  the exact .NET 10.0.11 runtime license and third-party notices.

## Release gate

Do not publish the next binary release until all of these are true:

1. the exact FFmpeg core and Flyleaf release sources plus the missing custom
   build recipe, .NET patches and complete linked-library source set are
   available beside the binary release;
2. the application icon and 37 legacy toolbar assets have completed provenance
   records or have been replaced;
3. the installer and ZIP visibly include the complete license set;
4. the application exposes an About/Legal entry with copyright, no-warranty,
   license, third-party notice, and source links.

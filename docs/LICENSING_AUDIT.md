# Licensing audit

Audit date: 2026-08-17  
Audited revision: working tree based on commit `a351e19`  
Scope: tracked source, project files, dependency locks, NuGet metadata,
application resources, installer, and release workflow.

This is an engineering compliance review, not legal advice.

## Recommendation

Use `GPL-3.0-only` for CryptoBook while the distributed application contains
`Sdcb.FFmpeg.runtime.windows-x64` 7.1.0. That runtime package explicitly
declares `GPL-3.0-only`, while Flyleaf components declare
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
| High (partially resolved) | The FFmpeg NuGet package contains DLLs but no corresponding source. The audit has now fixed the exact FFmpeg commit, embedded configuration, matching BtbN recipe, package/DLL hashes, and 86 declared dependency source pins. The original pruned BtbN binary archive URL/hash and a complete archived source set for all linked libraries are still absent. | Use the checked-in verifier and provenance snapshot, obtain the upstream archive evidence if possible, then assemble and retain every pinned dependency source and patch before the next binary release. |
| High | The release workflow packaged only `CryptoBook.exe`; the installer and ZIP did not include license texts or notices. | Update packaging so the installer and ZIP contain the license bundle. |
| Medium | A self-contained publish embeds .NET 8 runtime packs that do not appear in the project NuGet lock table. | Ship the exact .NET 8.0.29 license and runtime third-party notice captured from the pinned runtime pack. |
| Medium (partially resolved) | All 48 images now have a hash/metadata/Git inventory and contact sheet. Marketing composites rebuild byte-for-byte and the generated background has a prompt; the application icon and 37 legacy toolbar images still lack creator/license evidence. | Complete `compliance/assets/ATTESTATION.md` with supporting records or replace the unresolved icon families. |
| Resolved | The human contributor appeared in Git history under several display names but the same email. | On 2026-08-17 the project owner confirmed Романов Сергей as developer and copyright holder; this is now recorded in `COPYRIGHT.md` and build metadata. |
| Medium | Several runtime dependencies are prerelease/CI versions (`MaterialDesign*`, `Mono.Posix`, `SharpGen`, `Vortice*`). | Prefer stable releases and repeat license review on every dependency update. |
| Low | No SPDX header policy exists for source files. | Add `SPDX-License-Identifier: GPL-3.0-only` to project-authored source over time and require it for new files. |
| Positive | NuGet lock files, deterministic builds, NuGet audit, SPDX SBOM generation, checksums, and tagged release builds are already configured. | Keep these controls and add a license-compliance check to CI. |

## Dependency summary

Application/runtime lock:

| License | Packages |
| --- | --- |
| GPL-3.0-only | `Sdcb.FFmpeg.runtime.windows-x64` 7.1.0 |
| LGPL-3.0-or-later | `FlyleafLib` 3.10.4; `FlyleafLib.Controls.WPF` 1.6.4; `Flyleaf.FFmpeg.Bindings` 7.1.1 |
| Apache-2.0 | `SQLitePCLRaw.bundle_e_sqlite3`, `.core`, `.lib.e_sqlite3`, `.provider.e_sqlite3` 2.1.12 |
| MIT | Autofac family; Konscious Argon2/Blake2; MaterialDesign/Dragablz; Microsoft and System libraries; Mono.Posix; SharpGen; Vortice; WpfColorFontDialog |
| Public domain | SQLite native code embedded by the e_sqlite3 bundle |
| MIT and bundled notices | Microsoft .NET/Windows Desktop Runtime 8.0.29 included by self-contained publish |

Test-only additions are MIT, Apache-2.0, and MS-PL as enumerated in
`THIRD_PARTY_NOTICES.md`. Package names, versions, and direct/transitive status
remain authoritative in the committed lock files.

## Prepared license set

- `LICENSE` — GNU GPL version 3 text and project license;
- `COPYRIGHT.md` — project copyright statement;
- `THIRD_PARTY_NOTICES.md` — component versions, attributions, and licenses;
- `SOURCE_CODE.md` — corresponding-source location and unresolved FFmpeg duty;
- `ASSET_PROVENANCE.md` — asset rights register and outstanding verification;
- `compliance/ffmpeg/` — native binary manifest, exact source/build mapping,
  dependency source pins and upstream evidence request;
- `compliance/assets/` — image manifest, contact sheet and attestation draft;
- `LICENSES/` — LGPL-3.0-or-later, MIT, Apache-2.0, and MS-PL texts plus
  the exact .NET 8.0.29 runtime license and third-party notices.

## Release gate

Do not publish the next binary release until all of these are true:

1. the exact FFmpeg core/build recipe already identified here and the complete
   source/patch set for all enabled linked libraries are available beside the
   binary release;
2. the application icon and 37 legacy toolbar assets have completed provenance
   records or have been replaced;
3. the installer and ZIP visibly include the complete license set;
4. the application exposes an About/Legal entry with copyright, no-warranty,
   license, third-party notice, and source links.

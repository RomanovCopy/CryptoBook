# FFmpeg binary provenance

This record applies to `Sdcb.FFmpeg.runtime.windows-x64` 7.1.0 as restored
from NuGet and used by CryptoBook. Exact hashes and the configuration string
are in `package-manifest.json`.

## Confirmed facts

1. The NuGet package declares `GPL-3.0-only`. Its `.nupkg` and all eight DLLs
   have been hashed; `Test-FfmpegProvenance.ps1` verifies them.
2. Every DLL contains product version
   `n7.1-58-g10aaf84f85-20241215` and exports its FFmpeg library version,
   configuration and license. All eight exports report `GPL version 3 or
   later` and the same configure command.
3. The embedded abbreviated revision resolves uniquely to FFmpeg commit
   [`10aaf84f855dbcedb8ee2e3fce307e9b98320946`](https://github.com/FFmpeg/FFmpeg/commit/10aaf84f855dbcedb8ee2e3fce307e9b98320946),
   dated 2024-12-11. This is the exact FFmpeg tree for the native libraries.
4. Sdcb's `Sdcb.FFmpeg.NuGetBuilder` downloads a BtbN Windows shared archive,
   copies its DLLs and creates the runtime NuGet package. The historical BtbN
   recipe at commit
   [`dc38e41621fd62eec41a467dad15462efdb0d516`](https://github.com/BtbN/FFmpeg-Builds/commit/dc38e41621fd62eec41a467dad15462efdb0d516)
   builds the `win64 gpl-shared 7.1` variant with
   `./build.sh win64 gpl-shared 7.1`. Its generated feature set includes
   `libvvenc`, matching the embedded configuration. A later BtbN commit on
   2024-12-15 disabled `vvenc`, so that later recipe cannot have produced
   these DLLs.

The generated historical recipe is preserved as
`btbn-win64-gpl-shared-7.1.Dockerfile`; its SHA-256 and the 86 source references
declared by its 85 enabled stages are recorded in `source-pins.json`. The
verifier confirms that its feature configuration is a literal ordered subset
of the configuration exported by the DLLs (the remaining exported arguments
are BtbN's platform/toolchain flags).

## Provenance conclusion and limitation

The exact FFmpeg source commit, complete embedded configure parameters and the
matching BtbN build recipe are fixed. The original December 2024 daily BtbN
release asset is no longer present under the project's normal retention policy,
and Sdcb did not commit the 7.1.0 `PackageInfo` update or place source material
inside the NuGet package. Therefore the original archive URL and archive hash
remain unconfirmed; the binary hashes in this repository are the controlling
identity evidence.

This record is not, by itself, the complete Corresponding Source required when
conveying the DLLs. A release must also provide the source trees for all enabled
third-party libraries, including the exact revisions and patches selected by
the pinned BtbN recipe, plus the build scripts. Until that bundle is assembled
and archived with the release, FFmpeg source compliance remains an open release
gate.

The remaining acquisition and validation procedure is documented in
`CORRESPONDING_SOURCE_CHECKLIST.md`. This workstation had no Docker engine, so
the 82 required dependency source-cache archives were not downloaded locally.

## Verification

From the repository root, after restoring packages:

```powershell
pwsh -File tools/compliance/Test-FfmpegProvenance.ps1
```

The script independently hashes the package and DLLs, calls the exported
version/configuration/license functions, and compares every value with the
checked-in manifest.

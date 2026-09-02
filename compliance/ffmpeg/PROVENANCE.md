# FFmpeg binary provenance

This record applies to `CryptoBook.Flyleaf.FFmpeg.Runtime.Windows.X64`
9.0.20260816, the RID-native package used by CryptoBook. The package contains
the unmodified FFmpeg DLLs from the official Flyleaf v3.11.3 release. Exact
archive, package and library hashes are in `package-manifest.json`.

## Confirmed facts

1. The upstream archive is the official
   [`Flyleaf_v3.11.3.7z`](https://github.com/SuRGeoNix/Flyleaf/releases/tag/v3.11.3)
   asset, size 44,830,891 bytes and SHA-256
   `1280CB89C6C5BC6D7D776152274167651C92A0B83FC1507E7106C6CDEE3B1D18`.
2. The tagged Flyleaf tree at commit
   [`2e11026f0690c1707db70d84f199917d88c3a431`](https://github.com/SuRGeoNix/Flyleaf/commit/2e11026f0690c1707db70d84f199917d88c3a431)
   contains the same seven DLLs. CryptoBook only repackages those bytes into
   `runtimes/win-x64/native`; the local package and every DLL are hashed and
   verified by `Test-FfmpegProvenance.ps1`.
3. Every DLL identifies product version
   `N-126175-g0056dd32fd-20260816`, exports its FFmpeg API version, reports
   `GPL version 3 or later`, and exposes the same complete configure command.
4. The embedded abbreviated revision resolves uniquely to FFmpeg commit
   [`0056dd32fd94e739e14bb3c463c68ebe806dfd1d`](https://github.com/FFmpeg/FFmpeg/commit/0056dd32fd94e739e14bb3c463c68ebe806dfd1d),
   dated 2026-08-15. This is the exact FFmpeg core tree represented by the
   native libraries.
5. Flyleaf describes the runtime as a minimal Windows build for FFmpeg 9,
   without encoders and patched for a .NET native-exception issue. The exported
   configuration independently confirms the disabled encoders, shared-library
   build, GPL/version3 flags and enabled linked libraries.

## Local package reproduction

`tools/ffmpeg-runtime-package/New-FlyleafFfmpegRuntimePackage.ps1` downloads the
pinned upstream archive, validates its size and SHA-256, validates every native
library against `package-manifest.json`, and creates the local RID package.
`NuGet.config` maps only this package ID to `third_party/nuget`; all other
packages remain sourced from NuGet.org.

## Source availability boundary

The exact FFmpeg core source and exact Flyleaf release source are known and are
archived by `New-FfmpegCoreSourceSnapshot.ps1`. The official binary release and
tag do not include the custom FFmpeg build recipe, the referenced .NET patch
set, or exact source revisions for every statically linked dependency named by
the exported configuration. Those items remain an upstream evidence request.

Consequently, the checked-in evidence is stronger and more reproducible than
the former moving runtime dependency, but it is not by itself the complete
Corresponding Source required when conveying GPL native libraries. The release
gate remains open until the missing recipe, patches and linked-library source
set are obtained and retained with the release.

## Verification

From the repository root, after locked restore:

```powershell
pwsh -File tools/compliance/Test-FfmpegProvenance.ps1
```

The verifier checks the committed local-feed package against the restored
package, parses package identity and license metadata, hashes every DLL, calls
the exported version/configuration/license functions and compares every value
with the checked-in manifest.

# Source code availability

CryptoBook binary releases are built from a version tag in
<https://github.com/RomanovCopy/CryptoBook>. For a binary whose version is
`X.Y.Z` or `X.Y.Z.W`, the matching source is the tag `vX.Y.Z` or `vX.Y.Z.W`.
The tagged tree contains the project files, locked dependency graph, build
scripts, and release workflow used to produce the application.

The release page must make the matching source archive available from the same
location as the installer and ZIP. If CryptoBook is redistributed elsewhere,
the distributor must also satisfy the source-code requirements of GNU GPL v3
section 6; a link to an unrelated or moving branch is not a substitute for the
corresponding source of the conveyed binary.

CryptoBook 1.1.2.3 currently consumes the native package
`Sdcb.FFmpeg.runtime.windows-x64` 7.1.0, which declares `GPL-3.0-only`. Binary
inspection fixed the exact FFmpeg tree at commit
`10aaf84f855dbcedb8ee2e3fce307e9b98320946`, the matching BtbN recipe at
`dc38e41621fd62eec41a467dad15462efdb0d516`, and the complete embedded
configuration string. Hashes, evidence, 86 declared dependency source pins and
verification tooling are under `compliance/ffmpeg/` and `tools/compliance/`.

`New-FfmpegCoreSourceSnapshot.ps1` retrieves and hashes the exact FFmpeg core
tree and build-recipe tree. This small snapshot is intentionally labelled as
provenance material, not complete Corresponding Source: the GPL release bundle
must additionally contain the source trees and patches for every enabled linked
library selected by `compliance/ffmpeg/source-pins.json`. The original December
2024 BtbN binary archive is no longer publicly retained, and Sdcb's repository
does not contain the 7.1.0 input URL, so the original archive URL/hash remains
an upstream evidence request. The NuGet package alone contains DLLs, not source.

Build instructions are in `README.md`, `docs/PRODUCTION.md`, and
`.github/workflows/release.yml`. Dependency versions are in
`CryptoBook/packages.lock.json` and `CryptoBook.Tests/packages.lock.json`.
The self-contained .NET runtime source is maintained at
<https://github.com/dotnet/runtime>; its exact license and third-party notices
for the pinned runtime pack are shipped in `LICENSES/`.

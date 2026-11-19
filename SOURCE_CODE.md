# Source code availability

The bundled, unmodified 7-Zip 26.06 console engine has matching source at
<https://github.com/ip7z/7zip/releases/download/26.06/7z2606-src.tar.xz>.
Its official MSI/source digests and exact component hashes are recorded in
`third_party/7zip/provenance.json`; its license is included as
`LICENSES/7ZIP-26.06-LICENSE.txt`. The original 7-Zip project is
<https://www.7-zip.org/>. CryptoBook does not modify or rebuild this engine.

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

CryptoBook 1.1.5.6 consumes the local RID-native package
`CryptoBook.Flyleaf.FFmpeg.Runtime.Windows.X64` 9.0.20260816. It contains the
unmodified FFmpeg DLLs from the official Flyleaf v3.11.3 release. The libraries
report `GPL version 3 or later` and identify the exact FFmpeg tree at commit
`0056dd32fd94e739e14bb3c463c68ebe806dfd1d`. The official archive, local package,
all DLL hashes, the exact Flyleaf release commit and the complete embedded
configuration string are recorded under `compliance/ffmpeg/`.

`New-FfmpegCoreSourceSnapshot.ps1` retrieves and hashes the exact FFmpeg core
and Flyleaf release trees. This snapshot is intentionally labelled as
provenance material, not complete Corresponding Source: the upstream release
does not publish the custom FFmpeg build recipe, referenced .NET patch set or
exact revisions for every statically linked library. Those missing materials
remain an upstream evidence request documented in `compliance/ffmpeg/`.

Build instructions are in `README.md`, `docs/PRODUCTION.md`, and
`.github/workflows/release.yml`. Dependency versions are in
`CryptoBook/packages.lock.json` and `CryptoBook.Tests/packages.lock.json`.
The self-contained .NET runtime source is maintained at
<https://github.com/dotnet/runtime>; its exact license and third-party notices
for the pinned runtime pack are shipped in `LICENSES/`.

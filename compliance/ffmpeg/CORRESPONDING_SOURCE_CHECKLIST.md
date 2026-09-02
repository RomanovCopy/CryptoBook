# Completing the FFmpeg Corresponding Source bundle

CryptoBook fixes the official Flyleaf release archive, all native hashes, the
exact FFmpeg core commit, the Flyleaf release commit and the complete configure
string. The following work remains before distributing the FFmpeg 9 DLLs.

1. Generate and retain the two source archives already available:

   ```powershell
   pwsh -File tools/compliance/New-FfmpegCoreSourceSnapshot.ps1
   ```

   This captures the exact FFmpeg core and Flyleaf release trees and records
   their SHA-256 hashes.
2. Retain the committed local NuGet package, its package-construction project
   and script, `package-manifest.json`, and the official archive URL, size and
   SHA-256. These prove the exact binary-to-package transformation.
3. Obtain from the Flyleaf maintainer the custom FFmpeg build scripts, .NET
   patch set, toolchain/container definition and exact revisions of every
   statically linked library enabled by the exported configure command. The
   requested evidence is listed in `UPSTREAM_REQUEST.md`.
4. Archive the supplied sources, scripts and patches without modification and
   add an independently generated manifest containing each file's origin,
   revision, size and SHA-256.
5. Rebuild in a controlled environment. Compare every exported API version,
   license and configuration with `package-manifest.json`; document any
   bit-for-bit variance caused by toolchain or timestamp differences.
6. Include the complete source bundle or a valid GPL section 6 written offer
   with every distribution channel that conveys the DLLs.

Do not label the smaller `CryptoBook-ffmpeg-provenance.zip` as complete
Corresponding Source. It deliberately contains only the exact core/release
trees and the evidence currently available.

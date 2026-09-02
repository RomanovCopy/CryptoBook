# CryptoBook local NuGet feed

This directory contains pinned packages that are required for deterministic,
locked and offline-capable CryptoBook builds but are not published by their
upstream authors on NuGet.org.

`CryptoBook.Flyleaf.FFmpeg.Runtime.Windows.X64` repackages the unmodified
FFmpeg DLLs from the official Flyleaf v3.11.3 release into the standard
`runtimes/win-x64/native` layout. Rebuild it with:

```powershell
pwsh -File tools/ffmpeg-runtime-package/New-FlyleafFfmpegRuntimePackage.ps1
```

The script downloads the pinned upstream archive, verifies its SHA-256 and all
native-library hashes, then produces the local package. Detailed provenance is
under `compliance/ffmpeg/`.

# Flyleaf FFmpeg 9 runtime for CryptoBook

This package contains the seven unmodified Windows x64 FFmpeg shared libraries
from the official Flyleaf v3.11.3 archive:

- upstream release: <https://github.com/SuRGeoNix/Flyleaf/releases/tag/v3.11.3>
- upstream archive: `Flyleaf_v3.11.3.7z`
- archive SHA-256: `1280CB89C6C5BC6D7D776152274167651C92A0B83FC1507E7106C6CDEE3B1D18`
- Flyleaf source tag commit: `2e11026f0690c1707db70d84f199917d88c3a431`
- embedded FFmpeg commit: `0056dd32fd94e739e14bb3c463c68ebe806dfd1d`
- embedded product version: `N-126175-g0056dd32fd-20260816`

The native libraries report `GPL version 3 or later`. CryptoBook only changes
their package layout to `runtimes/win-x64/native`; the DLL bytes are preserved.
Exact hashes, exported versions, build configuration and the known source-code
availability boundary are recorded in CryptoBook's `compliance/ffmpeg/` data.

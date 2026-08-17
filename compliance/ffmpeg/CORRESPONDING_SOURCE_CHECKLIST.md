# Completing the FFmpeg Corresponding Source bundle

The repository already fixes the exact FFmpeg core tree, BtbN recipe,
configuration and dependency source pins. The following acquisition step still
has to be completed before distributing the native DLLs.

1. On a machine with Docker and Bash, clone
   `https://github.com/BtbN/FFmpeg-Builds.git` and switch to detached commit
   `dc38e41621fd62eec41a467dad15462efdb0d516`.
2. Set `GITHUB_REPOSITORY=BtbN/FFmpeg-Builds`, then run:

   ```bash
   ./generate.sh win64 gpl-shared 7.1
   ./download.sh
   ```

   `download.sh` may retrieve a superset. The checked-in Dockerfile identifies
   the 82 cache archives actually required by the matching build; those
   archives contain the 86 declared repository/revision inputs.
3. Validate cache completeness and independently hash every required archive:

   ```powershell
   pwsh -File tools/compliance/Test-BtbnSourceCache.ps1 `
     -CacheDirectory path/to/FFmpeg-Builds/.cache/downloads `
     -OutputPath artifacts/ffmpeg-source-cache-manifest.json
   ```

4. Preserve in one release-side bundle:

   - the 82 validated source-cache archives and generated cache manifest;
   - the exact FFmpeg core source archive produced by
     `New-FfmpegCoreSourceSnapshot.ps1`;
   - the exact BtbN recipe archive, generated Dockerfile, patches and all files
     in `compliance/ffmpeg/`;
   - any local modifications, if a released binary is ever rebuilt or patched.

5. Rebuild in a controlled environment and compare the exported versions,
   license and configuration with `package-manifest.json`. Bit-for-bit identity
   may depend on the historical toolchain and date, so a different DLL hash is
   not alone proof of different source; document all reproducibility variance.

The current workstation did not have Docker, so the 82 dependency archives
were not downloaded during the 2026-08-17 audit. Do not rename the smaller
`CryptoBook-ffmpeg-provenance.zip` to “Corresponding Source”; it deliberately
contains only the exact core tree, recipe and evidence.

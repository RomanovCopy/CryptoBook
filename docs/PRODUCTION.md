# Production release policy

CryptoBook releases must be produced by the GitHub Actions workflow from a
version tag that references the verified release commit. Local developer
builds are not production artifacts.

## Required repository settings

1. Protect `main` (or the current release branch) and disallow direct pushes.
2. Require the `verify` CI job and at least one approving review.
3. Require CODEOWNERS review for security, recovery, and workflow changes.
4. Enable two-factor authentication and secret scanning.
5. Before enabling release signing, create a `production` GitHub Environment
   with required reviewers.
6. Store the code-signing certificate and password only in that environment.

## Release requirements

- Restore dependencies with `--locked-mode`.
- Run all tests in `Release`.
- Publish on the pinned `windows-2022` image, which includes Inno Setup.
- Produce both the self-contained Windows x64 application and its installer.
- Sign the executable or installer with an Authenticode certificate when the
  signing secrets are configured.
- Publish SHA-256 checksums and an SBOM with the release.
- Include `LICENSE`, `COPYRIGHT.md`, `THIRD_PARTY_NOTICES.md`,
  `SOURCE_CODE.md`, `ASSET_PROVENANCE.md`, `LICENSES/`, and `compliance/` in
  both the installer and portable ZIP.
- Run `tools/compliance/Test-FfmpegProvenance.ps1` after locked restore; any
  native hash, version, license, or configure-string mismatch blocks release.
- Publish `CryptoBook-ffmpeg-provenance.zip` as supporting evidence. It contains
  the exact FFmpeg core and Flyleaf release trees, but does not replace the
  missing custom build recipe, patch set, and complete source bundle for all
  linked libraries.
- Publish the matching tagged CryptoBook source and the exact corresponding
  source/build materials for the bundled GPL-covered FFmpeg libraries beside
  every binary release.
- Publish an explicit signing-status file and warning for unsigned releases.
- Retain the previous version for rollback.

Unsigned releases are permitted while no production certificate is available.
Users must verify the published SHA-256 checksums before running them.

The built-in updater applies the same rule automatically. It offers automatic
installation only when the release contains the installer, `SHA256SUMS.txt`,
and `SIGNING-STATUS.txt`, and it always verifies the installer's SHA-256 before
launch. A release declared as signed must have a valid, trusted Authenticode
signature. An invalid signature or a declaration/signature mismatch is always
rejected. The production composition explicitly uses
`AllowWithVerifiedChecksum`, so an installer declared as unsigned is accepted
only after its checksum succeeds; other hosts default to
`RequireAuthenticodeSignature` unless they deliberately choose that policy.

Never put signing certificates, passwords, document contents, recovery files,
or user crash logs in the repository or CI artifacts.

## Recovery objectives

- Unsaved-document RPO: 15 seconds after the most recent edit.
- A failed save must leave the previous document and its `.bak` intact.
- A crash-recovery snapshot is encrypted with Windows DPAPI for the current
  user and is deleted only after a successful save or explicit discard.

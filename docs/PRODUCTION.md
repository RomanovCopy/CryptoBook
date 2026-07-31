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
- Publish on `windows-latest`.
- Sign the executable or installer with an Authenticode certificate when the
  signing secrets are configured.
- Publish SHA-256 checksums and an SBOM with the release.
- Publish an explicit signing-status file and warning for unsigned releases.
- Retain the previous version for rollback.

Unsigned releases are permitted while no production certificate is available.
Users must verify the published SHA-256 checksums before running them.

Never put signing certificates, passwords, document contents, recovery files,
or user crash logs in the repository or CI artifacts.

## Recovery objectives

- Unsaved-document RPO: 15 seconds after the most recent edit.
- A failed save must leave the previous document and its `.bak` intact.
- A crash-recovery snapshot is encrypted with Windows DPAPI for the current
  user and is deleted only after a successful save or explicit discard.

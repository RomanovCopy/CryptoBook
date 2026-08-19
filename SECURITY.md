# Security Policy

## Supported versions

Security fixes are targeted at the latest stable CryptoBook release. Older releases may be
unsupported unless a regression or migration issue requires a compatibility fix.

## Reporting a vulnerability

Please do not disclose a suspected vulnerability in a public GitHub issue.

Preferred reporting method: use GitHub's private vulnerability reporting feature for this
repository when it is available. Include:

- affected CryptoBook version;
- operating system version;
- reproduction steps or a minimal proof of concept;
- expected and observed behavior;
- whether protected files, passwords, keys or recovery data are involved;
- any suggested mitigation, if known.

If private vulnerability reporting is not available, contact the repository owner privately
through the contact method published on the owner's GitHub profile.

## Security scope

Security-sensitive areas include, but are not limited to:

- `.cbook` and legacy `.cbox` parsing, encryption and decryption;
- Argon2id key derivation and AES-256-GCM usage;
- password/key lifetime and in-memory key clearing;
- automatic key reset and lock snapshots;
- Windows DPAPI recovery data;
- atomic save, backup and recovery behavior;
- updater, release integrity and checksum verification;
- dependencies and bundled native media components.

## Limitations

CryptoBook is a desktop application, not a hardened password manager or full-disk encryption
product. Encryption protects supported files against unauthorized reading without the password,
but it does not replace operating-system security, backups, malware protection or physical
security.

A lost encryption password cannot be recovered by the application.

## Coordinated disclosure

Please allow reasonable time to reproduce, assess and fix a reported vulnerability before public
disclosure. Security fixes may be released without publishing exploit details when disclosure
would create unnecessary risk for users who have not yet updated.

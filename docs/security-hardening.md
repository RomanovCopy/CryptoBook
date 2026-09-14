# Password and recovery hardening

New file encryption requires a password of 8–128 characters, at least six
different case-folded characters, and rejects digits-only passwords, repeated
short patterns and several common password/keyboard constructions. Spaces are
allowed. This is a minimum policy, not an entropy measurement or a comprehensive
breached-password database. Prefer a unique randomly generated password or
randomly generated passphrase.

The policy is enforced in the file codec and in the production key provider,
including when a previously accepted decryption password remains cached.
Decryption accepts existing passwords; changing the input policy does not lock
users out of older files. Creating a new protected document or saving an opened
document asks for a new suitable key when necessary. Automatic recovery and lock
snapshots use the current session password, including an older weak password,
so the user can preserve work before choosing a new key.

## Recovery files

New recovery copies of protected sessions are authenticated with AES-256-GCM
and a password-derived Argon2id key. Windows account credentials alone cannot
decrypt these copies. An inactive protected document also requires password
protection for the workspace snapshot. Without its key, saving such a snapshot
fails instead of falling back to DPAPI. Unprotected documents use CurrentUser
DPAPI recovery even when an encryption password is cached for other files.

The snapshot envelope `CBSNAP03` contains an 8-byte magic, 16-byte random salt,
12-byte random nonce, ciphertext, and a 16-byte GCM tag. The first 36 bytes are
authenticated as AAD. Its fixed KDF parameters are Argon2id, 64 MiB, three passes,
four lanes, and a 32-byte output. These parameters are format constants; changing
future file-encryption defaults must not change the snapshot reader.

Older DPAPI recovery copies and V1/V2 lock snapshots remain readable. Existing
DPAPI copies do not acquire password protection simply by installing this update.
Recover the work and save with a new strong key; successful save/lock removes the
ordinary recovery copy. Canceling password entry or failing restoration preserves
the copy in a deferred file for a subsequent startup and continues opening the
application. New autosaves and normal closing do not delete the deferred copy.

## Locking and memory

CryptoBook starts without an application password. A pending protected snapshot
is shown as an optional restoration notice; it does not block normal work.
Settings > Security includes "Reset key now", the current key status, and a
Ctrl+L reminder. The shortcut works in both the main window and Settings. The
button is disabled when no key is set or a key transition is in progress; no
application restart is required.
Key reset leaves ordinary documents open, including unsaved changes. Only the
encrypted editor is saved and closed when another editor contains an ordinary
document. Restoring a snapshot explicitly requests its password and checks for
unsaved work before replacing documents. Earlier pending lock snapshots are
preserved when another encrypted document is closed, and become available after
the latest snapshot is restored.

The notice identifies the source document(s), full original path(s), snapshot
date, and the required key: the encryption key active when the snapshot was
created. The same explanation is displayed in the key-entry window. Closing
the notice hides that snapshot for the current application session without
deleting it. A newly created snapshot produces a new notice.

New snapshots carry a separate `.notice` file protected with CurrentUser DPAPI.
It contains only display identities and the timestamp, never document contents
or keys. A snapshot stamp (length, modification time and encrypted header prefix)
prevents accidental association with a replaced snapshot; this is display-only
information and is never used to authenticate a key or select a restoration
path. Windows account access can reveal these filenames, but cannot decrypt the
snapshot content. Details follow queued snapshots. Older snapshots or missing,
damaged, or mismatched details display the exact snapshot path and explain that
the source name is unavailable until decryption. Successful decryption refreshes
the display identities from the authenticated snapshot metadata.

The workspace is temporarily hidden and disabled while encrypted documents are
being saved and closed. Secondary windows are hidden and media windows are
closed. File opening and key-entry services reject operations during this
transition. Successful key reset makes the application available immediately,
without an automatic password dialog. If writing
or verifying the snapshot fails, the cached key is cleared and unsaved documents
remain behind the lock screen. A separate password-encrypted verifier permits
unlocking without relying on a disk snapshot; removing a snapshot does not make
an arbitrary password acceptable. Retained work is not replaced by an older
snapshot after unlocking. Closing through the lock screen is disabled while
unsaved encrypted work exists only in memory. This exceptional screen protects
the retained encrypted document; absence of a key alone never activates it.

The cached password is protected using CryptProtectMemory with SAME_PROCESS.
Owned plaintext password buffers are pinned and cleared after use. Unlocking
does not create an immutable password string. Sensitive stream buffers are
cleared on disposal and when reallocated; serialization buffers are cleared on
success and failure. These measures reduce exposure, but do not protect against
an attacker controlling the process while it decrypts data. See Microsoft's
[CryptProtectMemory documentation](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectmemory).

## Existing encrypted files

V1 PBKDF2-HMAC-SHA256 at 100,000 iterations is retained strictly for reading:
changing that parameter would make existing files unreadable. All new file
writes remain Argon2id/AES-256-GCM V2. Replacing a V1 file does not create a new
V1 `.bak`; replacing an existing V2 file retains the normal encrypted backup.
Pre-existing backups are not silently deleted.

Open an old file with its original password, choose a new unique strong key in
Settings, and save it in the protected format. Already copied V1 files, weak-key
V2 files, DPAPI recovery copies, plaintext exports, and external backups cannot
be retroactively secured. Reusing an old password in a new container retains
the risk from any old copies using that password. Offline guessing cannot be
eliminated for a portable password-encrypted file; its cost and password entropy
determine resistance.

## Validation

Regression tests cover password policy, native protected-memory storage and
cleanup, key replacement during derivation, snapshot tampering and wrong keys,
same-account DPAPI rejection of new protected recovery copies, no-key recovery
refusal, legacy migration with a new key, disk-failure locking, cancellation,
rendered workspace occlusion across failure/unlock states, keyless startup,
plain recovery with a cached key, deferred recovery, and ordinary-document
preservation with either editor active during key reset.

A separate synthetic WPF host also exercises the production MainWindow BAML and
dependency container, failure and success paths through lock/reset/restore, and
real document serialization. It uses isolated recovery services and paths.

# Android storage architecture

CryptoBook treats every storage object as a `StorageLocation` made of a provider id and an opaque object id. UI, clipboard and operation coordination must not parse the object id or assume that it is a Windows/Android path. Human-readable paths are metadata only.

## Capability boundary

Providers publish metadata and capabilities independently. The Android providers expose shared storage only and enable browse/read/write/create/rename/delete/copy/move/raw-stream and preview operations. Preview reads text and supported images through provider streams; it never passes a remote locator to Windows file APIs. External open, workspace search and monitoring remain absent. Full Windows properties, the detailed properties/hash report and batch rename require a local copy.

Provider-wide capabilities must be distinguished from per-object capabilities: `WpdStorageProvider.ToMetadata` and `AndroidStorageProvider.ToMetadata` add `Encrypt` for a writable file, but not for a remote folder. Do not infer that all encryption commands are unavailable from the provider-wide flag set, or promise recursive remote-folder encryption from a file's flag.

## Decrypting a file on the device

A protected file can be copied onto Android and explicitly decrypted there. The user confirmed this workflow on 29 October 2026; stage 7's local validation does not count that confirmation as a new hardware test.

`FileExplorerModel.CanExecute_DecryptCommand` accepts a readable remote **file** when a key is available, independently of `Encrypt`. The single-file pipeline prepares decryption, shows `DecryptionOptionsWindow`, and publishes through the storage facade. Replacing the source requires both `Write` and `Delete`; the result is placed in the device folder with the output extension. Its name can differ from the original protected name. The protected source is removed only after output publication succeeds. Keeping a decrypted copy uses the explicitly selected destination instead of promising that every Save As target is on the device.

The output is unprotected. Format conversion can lose formatting, especially TXT; review the format and replacement warnings before confirming. This file workflow does not imply recursive remote-directory decryption or local-journal recovery on Android.

Remote preview is a separate path: `FilePreviewContentSource` checks CryptoBook headers and performs authenticated decryption for **local** files only. Remote preview reads the original raw stream. A protected file on Android is therefore not automatically decrypted by selecting it in the preview panel.

Android deletion is permanent from the application's point of view. The explorer therefore requires explicit confirmation before deleting any non-local item.

## Transfers

`TransferEngine` selects the strategy:

- Local to Android: transport `push`;
- Android to Local: transport `pull`;
- Android to Android on one device: provider copy/move;
- Android device A to device B: temporary local staging;
- move between providers/devices: copy, verify type, directory composition, size and SHA-256 of readable file streams, then delete the source.

Cancellation or disconnection of a remote/mixed batch does not promise whole-batch rollback. Inspect both sides before retrying; a partial destination or already completed items can remain. The local transfer journal and Windows Recycle Bin are not a device-wide recovery mechanism. Start with copies of disposable data for hardware acceptance.

## Transports

`WpdStorageProvider` is the primary ordinary-user transport. It uses the Windows Portable Devices/MTP stack through `PortableStorageDevice`, requires neither ADB nor Android developer mode, and publishes `mtp://` locators. The Windows device id and provider-relative object path are base64url-encoded inside the opaque locator and are decoded only at the provider boundary. Windows exposes only the shared objects made available by Android's MTP storage model; protected application directories remain inaccessible.

`WindowsPortableDeviceBridge` implements browse, raw read/write streams, create, rename, permanent delete, copy and move. File and folder moves preserve the source until the destination has been copied, the directory composition matches and every file has the same size and SHA-256 checksum. `TransferEngine` stages transfers between two MTP devices through a unique temporary local directory and applies the same copy/verify/delete rule to cross-device moves.

The ADB bridge remains an optional separate provider for development and power users. It discovers `online`, `offline` and `unauthorized` states and limits its root to `/storage/emulated/0`. ADB can be bundled at `platform-tools/adb.exe`, selected with `CRYPTOBOOK_ADB_PATH`, or resolved from `PATH`. If ADB is absent, its provider simply publishes no roots and does not affect WPD or local storage.

Both remote providers enable bounded text/image preview through their raw streams and omit provider-wide external-open, workspace-search and monitoring capabilities. File-level protection capabilities and explicit decryption are described above. Deletion on either remote provider is permanent from CryptoBook's point of view and therefore passes through the explorer's irreversible-operation confirmation.

## Validation boundary (31 October 2026)

The new help articles are `devices.android`, `devices.transfers` and `devices.limits` in all four application languages. Their hardware acceptance remains open; see [VALIDATION_STAGE7.md](help/VALIDATION_STAGE7.md). Provider tests use controlled bridges and do not establish physical MTP, USB disconnection/reconnection, two-device transfer, or device-side decryption acceptance. Local file-manager recycling must not be generalized to device deletion, or to the separate direct-provider deletion routes in content search and the image player.

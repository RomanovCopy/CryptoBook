# Android storage architecture

CryptoBook treats every storage object as a `StorageLocation` made of a provider id and an opaque object id. UI, clipboard and operation coordination must not parse the object id or assume that it is a Windows/Android path. Human-readable paths are metadata only.

## Capability boundary

Providers publish metadata and capabilities independently. The Android providers expose shared storage only and enable browse/read/write/create/rename/delete/copy/move/raw-stream and preview operations. Preview reads text and supported images through provider streams; it never passes a remote locator to Windows file APIs. External open, encryption, search and monitoring remain absent.

Android deletion is permanent from the application's point of view. The explorer therefore requires explicit confirmation before deleting any non-local item.

## Transfers

`TransferEngine` selects the strategy:

- Local to Android: transport `push`;
- Android to Local: transport `pull`;
- Android to Android on one device: provider copy/move;
- Android device A to device B: temporary local staging;
- move between providers/devices: copy, verify type and total size, then delete the source.

## Transports

`WpdStorageProvider` is the primary ordinary-user transport. It uses the Windows Portable Devices/MTP stack through `PortableStorageDevice`, requires neither ADB nor Android developer mode, and publishes `mtp://` locators. The Windows device id and provider-relative object path are base64url-encoded inside the opaque locator and are decoded only at the provider boundary. Windows exposes only the shared objects made available by Android's MTP storage model; protected application directories remain inaccessible.

`WindowsPortableDeviceBridge` implements browse, raw read/write streams, create, rename, permanent delete, copy and move. File and folder moves preserve the source until the destination has been copied and its total size verified. `TransferEngine` stages transfers between two MTP devices through a unique temporary local directory and applies the same copy/verify/delete rule to cross-device moves.

The ADB bridge remains an optional separate provider for development and power users. It discovers `online`, `offline` and `unauthorized` states and limits its root to `/storage/emulated/0`. ADB can be bundled at `platform-tools/adb.exe`, selected with `CRYPTOBOOK_ADB_PATH`, or resolved from `PATH`. If ADB is absent, its provider simply publishes no roots and does not affect WPD or local storage.

Both remote providers enable bounded text/image preview through their raw streams and deliberately omit external open, encryption, search and monitoring capabilities. Deletion on either remote provider is permanent from CryptoBook's point of view and therefore always passes through the explorer's irreversible-operation confirmation.

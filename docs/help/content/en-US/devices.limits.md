# Device capabilities and limits

Commands depend on the selected source, object and permissions. Android provides additional file storage; it does not expose every feature of a local Windows disk.

## Before you start

> Deletion on Android is permanent from CryptoBook’s point of view. Local operation recovery and the Windows Recycle Bin cannot restore deleted device objects. Decrypted output is stored without protection.

| Action | Local folder | Android/MTP |
| --- | --- | --- |
| Browse, copy, move, create folders, rename | Subject to folder permissions | Subject to device and object permissions |
| Delete from the file manager | Confirmed Windows recycling | Confirmed permanent deletion |
| Workspace search, monitoring, batch rename | Available under their own conditions | Unsupported as local commands |
| External open, full properties and hashes | Available under their own conditions | Copy locally first |
| Text/image preview | Supported files | Stream access without automatic decryption |

## Steps

1. For full viewing or detailed properties, copy the device file to a local folder and use the copy.
2. To decrypt on the device itself, enter the appropriate key, select one readable protected file and choose **Decrypt selected items**.
3. In **Decrypt document**, choose an available output format and **Replace the protected source with the decrypted file** for output on the device. Write and delete permissions are required; review the warning before confirming.
4. To retain the protected original, choose a decrypted copy and explicitly check its destination.

## Check the result

After successful replacement the output is in the device folder. Its name and extension can differ from the protected file. Decrypting a readable remote file does not require a separate encryption capability.

## Cancel and return

Close the options before confirming to cancel. After writing, do not rely on universal undo; retain a separate protected copy when needed.

## If it did not work

A disabled command usually means an unsuitable object, missing key or insufficient permissions. Writable Android files can permit encryption/protected copies according to their permissions; that does not imply recursive encryption of remote folders. The full protection workflow is covered separately.

## Related topics

- [Detailed properties and hashes](help:files.properties)
- [Decrypt files](help:security.decrypt)

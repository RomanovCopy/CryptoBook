# Transfer files between a PC and Android

Local folders and devices use the familiar copy and move commands. Supported directions include PC → Android, Android → PC, within one device and between two devices.

## Before you start

> Start with **Copy** and retain the source. Disconnecting or cancelling does not guarantee rollback of all items already processed in a device batch. A partial destination may remain.

Check free space and permissions at both ends. Transfers between devices can also require temporary space on the PC.

## Steps

1. In the file manager, select source files. Choose **Copy**, browse to the destination folder and choose **Paste**.
2. For a move, use **Cut** and Paste, or the move command with a folder destination.
3. For duplicate names, review Replace, Skip, Keep both or Cancel carefully. Keeping both is a safer first step for important data.
4. Wait for the progress result. To stop, choose **Cancel** and wait for cancellation to finish.

## Check the result

After copying, inspect the destination and open a sample file. Cross-source/device moves delete the source after verification, including folder composition and SHA-256 of accessible streams. This is not a promise of whole-batch rollback for remote operations.

## Cancel and return

After cancellation or connection loss, inspect both sides. Reconnect and compare files before retrying; retain the only usable copy.

## If it did not work

Resolve space, permission or connection problems, reconnect and retry only the necessary items. Local recovery journals and the Windows Recycle Bin do not automatically extend to Android.

## Related topics

- [Copy and move files](help:files.transfer)
- [Resolve name conflicts](help:files.conflicts)
- [Device capabilities and limits](help:devices.limits)

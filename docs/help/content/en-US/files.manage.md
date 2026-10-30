# Open, rename and delete

The selected row determines which object a command uses. Keep a separate copy before changing important files. Local deletion asks for confirmation and retains objects for recovery in CryptoBook.

## Open or copy a path

Double-click a folder to enter it. For a file use **Open**: supported documents open in CryptoBook, while other supported actions can use an external application. **Open with** invokes the system application choice for one local file. **Show in File Explorer** shows a local object in Windows File Explorer. These shell actions require a suitable local object and Windows support.

**Copy path** puts one or more selected paths on separate lines in the Windows clipboard. It does not copy file contents. Inspect the selection and the local/device address before using the text elsewhere.

## Rename one object

1. Select exactly one file or folder and choose **Rename**.
2. Edit the name in the row. If you omit an extension, the original extension is retained automatically. An explicit extension replaces the old one. To intentionally remove an extension, use multi-rename with an empty extension mask.

![Step 2. Inline editing of a complete new filename](images/S21-a.png "Step 2. Inline editing of a complete new filename")

3. Press Enter to apply. Esc requests confirmation to abandon the edit; confirm that cancellation to restore the previous name. On failure the old name is restored and an error is shown.

After success, refresh and check the full path. Single renaming updates the matching active document and pinned/recent references; a separate warning can report a failed reference update. Folder trees and external shortcuts are not a universal document-link tracker. Two or more local files use the separate multi-rename tool.

## Delete

Select the local objects, choose **Delete** and confirm the selection. **Cancel** in the confirmation leaves files in place. Canceling the operation restores the local batch; if recovery is blocked, the message identifies the preserved data.

To undo a completed deletion, open the original folder and choose **Restore files** on the toolbar. For selections from several folders, the log is beside the first source object. Recovery also works after restarting, returns complete files and folders, and never overwrites an occupied name. Close programs using the files or free the original name, then retry.

Objects remain in hidden .cryptobook-operation-* folders beside the originals and use disk space until restored or until you explicitly remove those folders. Removing them in Windows File Explorer makes deletion permanent. This uses CryptoBook recovery, separate from the Windows Recycle Bin and editor history. Android and other remote sources retain their separate permanent-deletion warning.

## Related topics

- [Rename multiple files](help:files.batch)
- [Source capabilities and limits](help:devices.limits)
- [Two editors, renaming and closing](help:doc.switch)

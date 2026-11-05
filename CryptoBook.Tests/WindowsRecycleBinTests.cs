using CryptoBook.Services;
using CryptoBook.Validation;

using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using CryptoBook.DTO;
using CryptoBook.Interfaces;

using Xunit;

namespace CryptoBook.Tests;

[Collection(WindowsRecycleBinCollection.Name)]
public sealed class WindowsRecycleBinTests
{
    [Fact]
    public async Task DirectoryHandleWithoutDeleteSharing_PreservesFolderUntilHandleIsReleased()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string folder = Directory.CreateDirectory(Path.Combine(temp.Path, "locked-folder")).FullName;
        string file = Path.Combine(folder, "content.txt");
        File.WriteAllText(file, "disposable");
        var service = new WindowsRecycleBinService();
        FileOperationBatchResult? deleted = null;
        try
        {
            using(SafeFileHandle handle = CreateFile(folder, 0x80000000, 3, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                Assert.False(handle.IsInvalid);
                deleted = await service.RecycleBatchAsync([folder]);
                Assert.False(deleted.Success);
                Assert.Equal(0, deleted.CompletedCount);
                Assert.True(deleted.Failure?.ErrorMessage?.Contains("0x80270027", StringComparison.OrdinalIgnoreCase) == true,
                    deleted.Failure?.ErrorMessage);
                Assert.Equal("disposable", File.ReadAllText(file));
            }
            deleted = await service.RecycleBatchAsync([folder]);
            Assert.True(deleted.Success, deleted.Failure?.ErrorMessage);
            Assert.Equal(1, deleted.CompletedCount);
            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            string? recycled = deleted?.Results.FirstOrDefault(result => result.Success)?.AffectedPath;
            if(recycled is not null && Path.Exists(recycled) && !Path.Exists(folder))
                await RecycleBinValidation.RestoreAsync(recycled, folder);
        }
    }

    [Fact]
    public async Task SourceDeletePermissionDenied_PreservesFolderAndCanBeRecycledAfterPermissionsRestored()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        var parent = new DirectoryInfo(temp.Path);
        var folder = Directory.CreateDirectory(Path.Combine(temp.Path, "permission-denied"));
        string file = Path.Combine(folder.FullName, "content.txt");
        File.WriteAllText(file, "disposable");
        byte[] parentPermissions = parent.GetAccessControl().GetSecurityDescriptorBinaryForm();
        byte[] folderPermissions = folder.GetAccessControl().GetSecurityDescriptorBinaryForm();
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier user = identity.User!;
        var service = new WindowsRecycleBinService();
        FileOperationBatchResult? deleted = null;
        try
        {
            DirectorySecurity deniedParent = parent.GetAccessControl();
            deniedParent.AddAccessRule(new FileSystemAccessRule(user,
                FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Deny));
            parent.SetAccessControl(deniedParent);
            DirectorySecurity deniedFolder = folder.GetAccessControl();
            deniedFolder.AddAccessRule(new FileSystemAccessRule(user,
                FileSystemRights.Delete, AccessControlType.Deny));
            folder.SetAccessControl(deniedFolder);
            try
            {
                deleted = await service.RecycleBatchAsync([folder.FullName]);
                Assert.False(deleted.Success);
                Assert.Equal(0, deleted.CompletedCount);
                Assert.True(deleted.Failure?.ErrorMessage?.Contains("0x80270021", StringComparison.OrdinalIgnoreCase) == true,
                    deleted.Failure?.ErrorMessage);
                Assert.Equal("disposable", File.ReadAllText(file));
            }
            finally
            {
                RestorePermissions(folder, folderPermissions);
                RestorePermissions(parent, parentPermissions);
            }
            deleted = await service.RecycleBatchAsync([folder.FullName]);
            Assert.True(deleted.Success, deleted.Failure?.ErrorMessage);
            Assert.Equal(1, deleted.CompletedCount);
        }
        finally
        {
            // Восстановление прав и данных обязательно и при неожиданном результате Shell.
            RestorePermissions(parent, parentPermissions);
            string? recycled = deleted?.Results.FirstOrDefault(result => result.Success)?.AffectedPath;
            if(recycled is not null && Path.Exists(recycled) && !Path.Exists(folder.FullName))
                await RecycleBinValidation.RestoreAsync(recycled, folder.FullName);
            if(Directory.Exists(folder.FullName))
                RestorePermissions(folder, folderPermissions);
        }
    }

    private static void RestorePermissions(DirectoryInfo directory, byte[] descriptor)
    {
        var permissions = new DirectorySecurity();
        permissions.SetSecurityDescriptorBinaryForm(descriptor, AccessControlSections.Access);
        directory.SetAccessControl(permissions);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadOnlyFolderAndFiles_CanBeRecycledAndRestored(bool readOnlyFiles)
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string folder = Directory.CreateDirectory(Path.Combine(temp.Path, "readonly-folder")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(folder, "nested")).FullName;
        string file = Path.Combine(nested, "content.txt");
        File.WriteAllText(file, "disposable");
        File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.ReadOnly);
        File.SetAttributes(nested, File.GetAttributes(nested) | FileAttributes.ReadOnly);
        if(readOnlyFiles)
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        FileOperationBatchResult? deleted = null;
        try
        {
            deleted = await new WindowsRecycleBinService().RecycleBatchAsync([folder]);
            Assert.True(deleted.Success, deleted.Failure?.ErrorMessage);
            Assert.Equal(1, deleted.CompletedCount);
            Assert.Equal(0, deleted.PermanentlyDeletedCount);
            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            string? recycled = deleted?.Results.FirstOrDefault(result => result.Success)?.AffectedPath;
            if(recycled is not null && Path.Exists(recycled) && !Path.Exists(folder))
                await RecycleBinValidation.RestoreAsync(recycled, folder);
            if(File.Exists(file)) File.SetAttributes(file, FileAttributes.Normal);
            if(Directory.Exists(nested)) File.SetAttributes(nested, FileAttributes.Directory);
            if(Directory.Exists(folder)) File.SetAttributes(folder, FileAttributes.Directory);
        }
        Assert.Equal("disposable", File.ReadAllText(file));
    }

    [Fact]
    public async Task BatchRecyclesWholeSelectionAndRestoresEveryFile()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string[] paths = Enumerable.Range(0, 12).Select(index => temp.File($"batch-{index}.txt", $"content-{index}")).ToArray();
        var service = new WindowsRecycleBinService();
        RecycleBinDeletionPlan plan = await service.PlanDeletionAsync(paths);
        Assert.True(plan.Fits, "The disposable recycling test requires known free Recycle Bin capacity.");
        FileOperationBatchResult result = await service.RecycleBatchAsync(paths);
        try
        {
            Assert.True(result.Success, result.Failure?.ErrorMessage);
            Assert.Equal(paths.Length, result.CompletedCount);
            Assert.Equal(0, result.PermanentlyDeletedCount);
            Assert.All(paths, path => Assert.False(File.Exists(path)));
        }
        finally
        {
            for(int index = 0; index < result.Results.Count; index++)
            {
                string? recycled = result.Results[index].AffectedPath;
                if(recycled is not null && Path.Exists(recycled) && !Path.Exists(paths[index]))
                    await RecycleBinValidation.RestoreAsync(recycled, paths[index]);
            }
        }
        for(int index = 0; index < paths.Length; index++)
            Assert.Equal($"content-{index}", File.ReadAllText(paths[index]));
    }

    [Fact]
    public async Task PermanentBatchHonorsCancellationAfterFirstCompletedItem()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string[] paths = Enumerable.Range(0, 3).Select(index => temp.File($"permanent-{index}.txt", "disposable")).ToArray();
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterFirstProgress(cancellation);
        FileOperationBatchResult result = await new WindowsRecycleBinService()
            .DeletePermanentlyAsync(paths, progress, cancellation.Token);
        Assert.True(result.Canceled);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(1, result.PermanentlyDeletedCount);
        Assert.Equal(2, paths.Count(File.Exists));
    }

    [Fact]
    public async Task PermanentBatchErrorPreservesCompletedCountAndUsesPermanentErrorMessage()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string first = temp.File("first.txt", "disposable");
        string locked = temp.File("locked.txt", "disposable");
        string last = temp.File("last.txt", "disposable");
        using var handle = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        FileOperationBatchResult result = await new WindowsRecycleBinService().DeletePermanentlyAsync([first, locked, last]);
        Assert.False(result.Success);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(1, result.PermanentlyDeletedCount);
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(locked));
        Assert.True(File.Exists(last));
        Assert.Contains(CryptoBook.Infrastructure.LocalizationManager.GetString("Explorer.PermanentDeleteFailed"), result.Failure!.ErrorMessage);
    }

    [Fact]
    public void PermanentSinkRequiresExplicitPermanentMode()
    {
        var recycle = new WindowsRecycleBinService.RecyclingProgressSink(CancellationToken.None);
        Assert.Equal(WindowsRecycleBinService.NotSupported, recycle.PreDeleteItem(0, null!));
        var permanent = new WindowsRecycleBinService.RecyclingProgressSink(CancellationToken.None, permanent: true);
        Assert.Equal(0, permanent.PreDeleteItem(0, null!));
        permanent.PostDeleteItem(0, null!, 0, null);
        Assert.True(permanent.Completed);
        Assert.True(permanent.PermanentlyDeleted);
    }

    private sealed class CancelAfterFirstProgress(CancellationTokenSource cancellation): IProgressReporter
    {
        public void Report(double? value, string? currentInfo = null)
        {
            if(value > 0) cancellation.Cancel();
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileAndWholeFolderEnterWindowsBinAndRestoreAtOriginalPath(bool directory)
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string original = Path.Combine(temp.Path, "Recycle-" + Guid.NewGuid().ToString("N") + (directory ? "" : ".txt"));
        if(directory) Directory.CreateDirectory(Path.Combine(original, "child"));
        string file = directory ? Path.Combine(original, "child", "known.txt") : original;
        File.WriteAllText(file, "Disposable recycle test Ж ä");
        var result = await new WindowsRecycleBinService().RecycleAsync(original);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.AffectedPath);
        try
        {
            Assert.False(Path.Exists(original));
            Assert.True(Path.Exists(result.AffectedPath));
            Assert.Empty(Directory.GetDirectories(temp.Path, LocalOperationJournal.Prefix + "*"));
        }
        finally { await RecycleBinValidation.RestoreAsync(result.AffectedPath!, original); }
        Assert.Equal("Disposable recycle test Ж ä", File.ReadAllText(file));
    }

    [Fact]
    public void ShellCallbackRefusesPermanentDeletionAndHonorsCancellation()
    {
        var sink = new WindowsRecycleBinService.RecyclingProgressSink(CancellationToken.None);
        Assert.Equal(WindowsRecycleBinService.NotSupported, sink.PreDeleteItem(0, null!));
        Assert.True(sink.RefusedPermanentDeletion);
        Assert.Equal(0, new WindowsRecycleBinService.RecyclingProgressSink(CancellationToken.None)
            .PreDeleteItem(WindowsRecycleBinService.RecycleTransferFlag, null!));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(WindowsRecycleBinService.Cancelled,
            new WindowsRecycleBinService.RecyclingProgressSink(cancel.Token).PreDeleteItem(0x80, null!));
    }

    [Fact]
    public async Task MissingItemReturnsFailureWithoutCreatingRecoveryFolders()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        var result = await new WindowsRecycleBinService().RecycleAsync(Path.Combine(temp.Path, "missing.txt"));
        Assert.False(result.Success);
        Assert.Empty(Directory.GetFileSystemEntries(temp.Path));
    }

    [Fact]
    public void RecoveryAvailabilityExcludesLiveJournalAndClearsAfterRestore()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string file = temp.File("original.txt", "original");
        Assert.False(LocalOperationJournal.HasRecovery(temp.Path));
        using(LocalOperationJournal.Create(temp.Path)) { }
        Assert.False(LocalOperationJournal.HasRecovery(temp.Path));
        using(var journal = LocalOperationJournal.Create(temp.Path))
        {
            journal.Move(file, journal.Allocate(file));
            Assert.False(LocalOperationJournal.HasRecovery(temp.Path));
        }
        Assert.True(LocalOperationJournal.HasRecovery(temp.Path));
        Assert.Empty(LocalOperationJournal.Recover(temp.Path));
        Assert.False(LocalOperationJournal.HasRecovery(temp.Path));
        Assert.Equal("original", File.ReadAllText(file));
    }
}

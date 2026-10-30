using CryptoBook.Services;
using CryptoBook.Validation;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class WindowsRecycleBinTests
{
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

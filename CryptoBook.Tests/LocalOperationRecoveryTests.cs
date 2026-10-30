using CryptoBook.DTO;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class LocalOperationRecoveryTests
{
    [Fact]
    public void InterruptedReplacementRestoresBothObjectsFromDurableJournal()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = temp.File("source.txt", "new"), target = temp.File("target.txt", "old");
        using(var journal = LocalOperationJournal.Create(temp.Path))
        {
            string staged = journal.Allocate(source);
            journal.Move(source, staged);
            journal.Move(target, journal.Allocate(target));
            journal.Move(staged, target);
        } // Simulated process exit: no rollback or completion.
        Assert.Empty(LocalOperationJournal.Recover(temp.Path));
        Assert.Equal("new", File.ReadAllText(source));
        Assert.Equal("old", File.ReadAllText(target));
        Assert.Empty(Directory.GetDirectories(temp.Path));
    }

    [Fact]
    public void OccupiedOriginalNamePreservesBothObjectsAndRecoveryCanBeRetried()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = temp.File("source.txt", "original");
        string saved;
        using(var journal = LocalOperationJournal.Create(temp.Path))
        {
            saved = journal.Allocate(source);
            journal.Move(source, saved);
            journal.Complete(retainForRestore: true);
        }
        temp.File("source.txt", "external");
        Assert.NotEmpty(LocalOperationJournal.Recover(temp.Path));
        Assert.Equal("external", File.ReadAllText(source));
        Assert.Equal("original", File.ReadAllText(saved));
        File.Delete(source);
        Assert.Empty(LocalOperationJournal.Recover(temp.Path));
        Assert.Equal("original", File.ReadAllText(source));
    }

    [Fact]
    public void ReplacedRecoveryObjectIsNotMistakenForOriginalFile()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = temp.File("source.txt", "original");
        string saved;
        using(var journal = LocalOperationJournal.Create(temp.Path))
        {
            saved = journal.Allocate(source);
            journal.Move(source, saved);
        }
        // Keep the original alive so its NTFS file ID cannot be reused.
        File.Move(saved, Path.Combine(temp.Path, "original-preserved.txt"));
        File.WriteAllText(saved, "external");
        Assert.NotEmpty(LocalOperationJournal.Recover(temp.Path));
        Assert.False(File.Exists(source));
        Assert.Equal("external", File.ReadAllText(saved));
    }

    [Fact]
    public void RecoverySkipsLiveOperationAndCanRunAfterLeaseCloses()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = temp.File("source.txt", "original");
        using(var journal = LocalOperationJournal.Create(temp.Path))
        {
            journal.Move(source, journal.Allocate(source));
            Assert.Empty(LocalOperationJournal.Recover(temp.Path));
            Assert.False(File.Exists(source));
        }
        Assert.Empty(LocalOperationJournal.Recover(temp.Path));
        Assert.Equal("original", File.ReadAllText(source));
    }

    [Fact]
    public void RecoveryAcceptsDifferentPathCasingAndMissingLeaseFile()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = temp.File("source", "original");
        using(var journal = LocalOperationJournal.Create(temp.Path)) journal.Move(source, journal.Allocate(source));
        string folder = Assert.Single(Directory.GetDirectories(temp.Path));
        File.Delete(Path.Combine(folder, "active.lock"));
        Assert.Empty(LocalOperationJournal.Recover(temp.Path.ToLowerInvariant()));
        Assert.Equal("original", File.ReadAllText(source));
        Assert.Empty(Directory.GetDirectories(temp.Path));
    }

    [Fact]
    public async Task IncompleteBatchRenameCanBeRestoredAfterRestart()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = temp.File("source.txt", "original");
        using var cancel = new CancellationTokenSource();
        var result = await new BatchRenameService().RenameAsync([new(source, "target.txt")],
            new CallbackProgress(_ => { temp.File("source.txt", "external"); cancel.Cancel(); }), cancel.Token);
        Assert.False(result.Success);
        string saved = Assert.Single(result.ChangedPaths).Value;
        File.Delete(source);
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.Empty(LocalOperationJournal.Recover(temp.Path, mapping));
        Assert.Equal(source, mapping[saved]);
        Assert.Equal("original", File.ReadAllText(source));
    }

    [Theory]
    [InlineData("new", ".txt", "new.txt")]
    [InlineData("new.md", ".txt", "new.md")]
    [InlineData("new", "", "new")]
    public void InlineRenamePreservesExtensionUnlessUserSuppliesOne(string typed, string old, string expected) =>
        Assert.Equal(expected, FileNameValidation.RenameFile(typed, old));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyVerificationChecksBytesAndDirectoryTree(bool directory)
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = Path.Combine(temp.Path, "source"), target = Path.Combine(temp.Path, "target");
        if(directory)
        {
            Directory.CreateDirectory(source); Directory.CreateDirectory(target);
            Directory.CreateDirectory(Path.Combine(source, "empty")); Directory.CreateDirectory(Path.Combine(target, "empty"));
        }
        string a = directory ? Path.Combine(source, "data") : source;
        string b = directory ? Path.Combine(target, "data") : target;
        File.WriteAllText(a, "original"); File.WriteAllText(b, "original");
        await LocalFileOperationService.VerifyCopyAsync(source, target, CancellationToken.None);
        File.WriteAllText(b, "tampered"); // Same length: length alone is insufficient.
        await Assert.ThrowsAsync<IOException>(() => LocalFileOperationService.VerifyCopyAsync(source, target, CancellationToken.None));
    }

    [Fact]
    public void CommittedReplacementCleansReadOnlyBackup()
    {
        using var temp = new BatchRenameTests.TestDirectory();
        string source = temp.File("source", "original");
        File.SetAttributes(source, File.GetAttributes(source) | FileAttributes.ReadOnly);
        using(var journal = LocalOperationJournal.Create(temp.Path))
        {
            journal.Move(source, journal.Allocate(source));
            journal.Complete();
        }
        Assert.Empty(Directory.GetFileSystemEntries(temp.Path));
    }

    private sealed class CallbackProgress(Action<int> callback) : IProgress<int>
    {
        public void Report(int value) => callback(value);
    }
}

using CryptoBook.Interfaces;
using CryptoBook.Services;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CryptoBook.Tests;

public sealed class LockSnapshotNoticeTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));

    public LockSnapshotNoticeTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void DocumentIdentityIsAvailableWithoutEncryptionKey_ButSidecarHasNoPlaintextOrContents()
    {
        string path = CreateSnapshot();
        var metadata = new LockSnapshotMetadata(Path.Combine(directory, "private-book.cbook"), "private-book.cbook",
            "Encrypted file", true, DateTimeOffset.UtcNow)
        {
            InactiveDocument = new("second.cbook", "second.cbook", "Encrypted file", 1, 0, false,
                Encoding.UTF8.GetBytes("secret document contents"))
        };
        LockSnapshotNoticeStore.Save(path, metadata);
        var notice = new LockSnapshotNoticeStore().Read(path)!;
        Assert.Equal(metadata.CreatedUtc, notice.SavedAt);
        Assert.Equal(2, notice.Documents.Count);
        Assert.Equal(metadata.OriginalPath, notice.Documents[0].OriginalPath);
        Assert.Equal("second.cbook", notice.Documents[1].DocumentName);
        byte[] stored = File.ReadAllBytes(path + ".notice");
        Assert.DoesNotContain("private-book", Encoding.UTF8.GetString(stored));
        byte[] plaintext = ProtectedData.Unprotect(stored, "CryptoBook.LockSnapshotNotice.v1"u8.ToArray(), DataProtectionScope.CurrentUser);
        try
        {
            Assert.DoesNotContain("secret document contents", Encoding.UTF8.GetString(plaintext));
            Assert.DoesNotContain("Content", Encoding.UTF8.GetString(plaintext));
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    [Fact]
    public void DismissalHidesOnlyCurrentSnapshot_DoesNotDeleteIt_AndNewSnapshotIsVisible()
    {
        string path = CreateSnapshot();
        byte[] original = File.ReadAllBytes(path);
        var store = new LockSnapshotNoticeStore();
        Assert.NotNull(store.Read(path));
        store.Dismiss(path);
        Assert.Null(store.Read(path));
        Assert.Null(store.Read(path));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.NotNull(new LockSnapshotNoticeStore().Read(path));
        DateTime modified = File.GetLastWriteTimeUtc(path);
        File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(original.Length));
        File.SetLastWriteTimeUtc(path, modified);
        Assert.NotNull(store.Read(path));
    }

    [Fact]
    public void LegacyDamagedOrStaleDetails_ShowActualSnapshotPathWithoutInventingDocumentName()
    {
        string path = CreateSnapshot();
        var store = new LockSnapshotNoticeStore();
        Assert.Empty(store.Read(path)!.Documents);
        File.WriteAllText(path + ".notice", "damaged");
        Assert.Empty(store.Read(path)!.Documents);
        LockSnapshotNoticeStore.Save(path, new("source.cbook", "source.cbook", "Encrypted file", true, DateTimeOffset.UtcNow));
        Assert.Single(store.Read(path)!.Documents);
        string other = Path.Combine(directory, "other.lock.cbook");
        File.WriteAllBytes(other, RandomNumberGenerator.GetBytes(128));
        File.Copy(path + ".notice", other + ".notice");
        var notice = store.Read(other)!;
        Assert.Equal(other, notice.SnapshotPath);
        Assert.Empty(notice.Documents);
    }

    private string CreateSnapshot()
    {
        string path = Path.Combine(directory, "last.lock.cbook");
        File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(128));
        return path;
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}

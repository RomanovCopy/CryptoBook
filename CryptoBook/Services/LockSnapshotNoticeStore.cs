using CryptoBook.Interfaces;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace CryptoBook.Services;

/// <summary>Display-only document identities. No document contents or passwords are stored here.</summary>
internal sealed class LockSnapshotNoticeStore
{
    private static readonly byte[] Entropy = "CryptoBook.LockSnapshotNotice.v1"u8.ToArray();
    private SnapshotStamp? dismissed;

    internal LockSnapshotNotice? Read(string path)
    {
        try
        {
            SnapshotStamp stamp = GetStamp(path);
            if(stamp == dismissed) return null;
            var fallback = new LockSnapshotNotice(path, new DateTimeOffset(new DateTime(stamp.ModifiedUtcTicks, DateTimeKind.Utc)), []);
            string detailsPath = path + ".notice";
            if(!File.Exists(detailsPath) || new FileInfo(detailsPath).Length > 65536)
                return fallback;
            try
            {
                byte[] plaintext = ProtectedData.Unprotect(File.ReadAllBytes(detailsPath), Entropy, DataProtectionScope.CurrentUser);
                try
                {
                    var details = JsonSerializer.Deserialize<NoticeEnvelope>(plaintext);
                    return details is { Version: 1, Documents.Length: > 0 and <= 2 } && details.Stamp == stamp &&
                        details.Documents.All(document => document is not null && !string.IsNullOrWhiteSpace(document.DocumentName))
                        ? new LockSnapshotNotice(path, details.SavedAt, details.Documents)
                        : fallback;
                }
                finally { CryptographicOperations.ZeroMemory(plaintext); }
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
            { return fallback; }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        { return null; }
    }

    internal void Dismiss(string path)
    {
        try { dismissed = GetStamp(path); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
    }

    internal static void Save(string path, LockSnapshotMetadata metadata)
    {
        string temporary = path + ".notice." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var documents = new List<LockSnapshotDocumentInfo>
            { new(metadata.DocumentName, metadata.OriginalPath) };
            if(metadata.InactiveDocument is { } inactive)
                documents.Add(new(inactive.DisplayName, inactive.FilePath));
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
                new NoticeEnvelope(1, GetStamp(path), metadata.CreatedUtc, documents.ToArray()));
            try
            {
                byte[] protectedDetails = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(temporary, protectedDetails);
                File.Move(temporary, path + ".notice", overwrite: true);
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        // Identity information is optional; a failure must never discard the encrypted snapshot.
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or CryptographicException) { }
        finally { DeleteFile(temporary); }
    }

    internal static void Copy(string from, string to)
    {
        try { if(File.Exists(from + ".notice")) File.Copy(from + ".notice", to + ".notice", overwrite: true); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
    }

    internal static void Delete(string path) => DeleteFile(path + ".notice");

    private static void DeleteFile(string path)
    {
        try { File.Delete(path); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
    }

    private static SnapshotStamp GetStamp(string path)
    {
        var file = new FileInfo(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Span<byte> prefix = stackalloc byte[48];
        int read = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        return new(file.Length, file.LastWriteTimeUtc.Ticks, Convert.ToBase64String(prefix[..read]));
    }

    private sealed record SnapshotStamp(long Length, long ModifiedUtcTicks, string Prefix);
    private sealed record NoticeEnvelope(int Version, SnapshotStamp Stamp, DateTimeOffset SavedAt,
        LockSnapshotDocumentInfo[] Documents);
}

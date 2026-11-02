using CryptoBook.Infrastructure;

using Microsoft.Win32.SafeHandles;

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CryptoBook.Services;

/// <summary>
/// Write-ahead log for local renames. Recovery never overwrites a name and checks
/// volume/file IDs, so a new object at an old path is not mistaken for our file.
/// Each operation owns private folders beside the affected objects (same volume).
/// </summary>
internal sealed class LocalOperationJournal : IDisposable
{
    internal const string Prefix = ".cryptobook-operation-";
    private readonly FileStream lease;
    private readonly string folder;
    private readonly State state;
    private string Manifest => Path.Combine(folder, "journal.json");
    internal bool IsCommitted => state.Committed;

    private LocalOperationJournal(string folder, FileStream lease, State state)
    {
        this.folder = folder;
        this.lease = lease;
        this.state = state;
    }

    internal static LocalOperationJournal Create(string parent)
    {
        string id = Guid.NewGuid().ToString("N");
        string folder = Path.Combine(parent, Prefix + id);
        Directory.CreateDirectory(folder);
        File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.Hidden);
        var journal = new LocalOperationJournal(folder,
            new FileStream(Path.Combine(folder, "active.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None), new State { Id = id, Folders = [folder] });
        journal.Save();
        return journal;
    }

    internal string Allocate(string besidePath)
    {
        string parent = Path.GetDirectoryName(besidePath)!;
        string owned = Path.Combine(parent, Prefix + state.Id);
        if(!state.Folders.Contains(owned, StringComparer.OrdinalIgnoreCase))
        {
            if(Path.Exists(owned)) throw new IOException(owned);
            state.Folders.Add(owned);
            Save();
            Directory.CreateDirectory(owned);
            File.SetAttributes(owned, File.GetAttributes(owned) | FileAttributes.Hidden);
        }
        return Path.Combine(owned, Guid.NewGuid().ToString("N"));
    }

    internal void Move(string source, string destination, string? expectedIdentity = null)
    {
        if(Path.Exists(destination)) throw new IOException(destination);
        string identity = Identity(source);
        if(expectedIdentity is not null && identity != expectedIdentity) throw new IOException(source);
        state.Moves.Add(new MoveEntry(source, destination, identity));
        Save(); // Durable BEFORE the filesystem mutation; recovery handles either outcome.
        MoveCore(source, destination);
        if(Identity(destination) != identity) throw new IOException(destination);
    }

    internal void Complete(bool retainForRestore = false)
    {
        state.Committed = !retainForRestore;
        state.Deleted = retainForRestore;
        Save();
        if(!retainForRestore) Cleanup();
    }

    internal string? Rollback(Dictionary<string, string>? restoredPaths = null)
    {
        var errors = new List<string>();
        for(int i = state.Moves.Count - 1; i >= 0; i--)
        {
            MoveEntry move = state.Moves[i];
            try
            {
                if(Path.Exists(move.From) && Identity(move.From) == move.Identity)
                {
                    // The forward move never ran, or recovery ran before a crash.
                }
                else
                {
                    if(!Path.Exists(move.To) || Identity(move.To) != move.Identity)
                        throw new IOException(move.To);
                    if(Path.Exists(move.From)) throw new IOException(move.From);
                    MoveCore(move.To, move.From);
                }
                if(restoredPaths is not null)
                {
                    foreach(string key in restoredPaths.Where(p => string.Equals(p.Value, move.To,
                        StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).ToArray())
                        restoredPaths[key] = move.From;
                    restoredPaths[move.To] = move.From;
                }
                state.Moves.RemoveAt(i);
                Save();
            }
            catch(Exception e) { errors.Add(e.Message); }
        }
        if(errors.Count == 0)
        {
            try { Cleanup(); }
            catch(Exception e) { errors.Add(e.Message); }
        }
        return errors.Count == 0 ? null : LocalizationManager.GetString("Explorer.RecoveryRequired") +
            Environment.NewLine + folder + Environment.NewLine + string.Join(Environment.NewLine, errors);
    }

    internal static IReadOnlyList<string> Recover(string parent, Dictionary<string, string>? restoredPaths = null)
    {
        var errors = new List<string>();
        foreach(string folder in Directory.EnumerateDirectories(parent, Prefix + "*")
            .OrderByDescending(Directory.GetCreationTimeUtc).ToArray())
        {
            if((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
            string manifest = Path.Combine(folder, "journal.json");
            if(!File.Exists(manifest)) continue; // auxiliary folder; its owner holds the log
            FileStream lease;
            try { lease = new FileStream(Path.Combine(folder, "active.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None); }
            catch(IOException e) when((e.HResult & 0xffff) is 32 or 33) { continue; } // Live operation.
            catch(Exception e) { errors.Add(folder + Environment.NewLine + e.Message); continue; }
            using(lease)
            {
                try
                {
                    State state = JsonSerializer.Deserialize<State>(File.ReadAllBytes(manifest))
                        ?? throw new IOException(manifest);
                    Validate(folder, state);
                    using var journal = new LocalOperationJournal(folder, lease, state);
                    if(state.Committed) journal.Cleanup();
                    else if(journal.Rollback(restoredPaths) is { } error) errors.Add(error);
                }
                catch(Exception e) { errors.Add(folder + Environment.NewLine + e.Message); }
            }
        }
        return errors;
    }

    internal static bool HasRecovery(string parent)
    {
        try
        {
            foreach(string folder in Directory.EnumerateDirectories(parent, Prefix + "*"))
            {
                if((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0 ||
                   !File.Exists(Path.Combine(folder, "journal.json"))) continue;
                try
                {
                    using var lease = new FileStream(Path.Combine(folder, "active.lock"), FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.None);
                    State? state = JsonSerializer.Deserialize<State>(File.ReadAllBytes(Path.Combine(folder, "journal.json")));
                    if(state is not null)
                    {
                        Validate(folder, state);
                        if(state.Moves.Count > 0) return true;
                    }
                }
                catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { }
            }
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
        return false;
    }

    private static void Validate(string folder, State state)
    {
        if(!Guid.TryParseExact(state.Id, "N", out _) ||
           !state.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            throw new IOException(folder);
        foreach(string owned in state.Folders)
            if(!Path.IsPathFullyQualified(owned) || Path.GetFileName(owned) != Prefix + state.Id ||
               (Directory.Exists(owned) && (File.GetAttributes(owned) & FileAttributes.ReparsePoint) != 0))
                throw new IOException(owned);
        foreach(MoveEntry move in state.Moves)
            if(!(state.Folders.Any(owned => IsOwnedChild(owned, move.From)) &&
                 state.Folders.Any(owned => IsOwnedChild(owned, move.To))) && !state.Folders.Any(owned =>
                (IsOwnedChild(owned, move.From) && IsAdjacent(owned, move.To)) ||
                (IsOwnedChild(owned, move.To) && IsAdjacent(owned, move.From))))
                throw new IOException(folder);
    }

    private static bool IsOwnedChild(string owned, string path) =>
        string.Equals(Path.GetDirectoryName(path), owned, StringComparison.OrdinalIgnoreCase);
    private static bool IsAdjacent(string owned, string path) =>
        IsOwnedChild(owned, path) || string.Equals(Path.GetDirectoryName(path),
            Path.GetDirectoryName(owned), StringComparison.OrdinalIgnoreCase);

    private void Save()
    {
        string temporary = Manifest + ".new";
        using(var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, state);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, Manifest, overwrite: true);
    }

    private void Cleanup()
    {
        // Main log is removed last. Failed cleanup remains retryable as a committed log.
        foreach(string owned in state.Folders.Where(p => !string.Equals(p, folder, StringComparison.OrdinalIgnoreCase)))
            if(Directory.Exists(owned)) DeleteOwned(owned);
        foreach(string child in Directory.EnumerateFileSystemEntries(folder))
        {
            if(Path.GetFileName(child) is "journal.json" or "active.lock") continue;
            DeleteOwned(child);
        }
        File.Delete(Manifest);
        lease.Dispose();
        File.Delete(Path.Combine(folder, "active.lock"));
        Directory.Delete(folder);
    }

    private static void MoveCore(string source, string destination)
    {
        if((File.GetAttributes(source) & FileAttributes.Directory) != 0)
            Directory.Move(source, destination);
        else File.Move(source, destination);
    }

    private static void DeleteOwned(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if((attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.ReparsePoint) == 0)
            foreach(string child in Directory.EnumerateFileSystemEntries(path)) DeleteOwned(child);
        if((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        if((attributes & FileAttributes.Directory) != 0) Directory.Delete(path);
        else File.Delete(path);
    }

    internal static string Identity(string path)
    {
        using SafeFileHandle handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if(handle.IsInvalid || !GetFileInformationByHandle(handle, out var info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return $"{info.VolumeSerialNumber:X8}:{info.FileIndexHigh:X8}{info.FileIndexLow:X8}";
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    public void Dispose() => lease.Dispose();
    public sealed class State
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Folders { get; set; } = [];
        public List<MoveEntry> Moves { get; set; } = [];
        public bool Committed { get; set; }
        public bool Deleted { get; set; }
    }
    public sealed record MoveEntry(string From, string To, string Identity);
}

using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Services;

public sealed partial class ArchiveService
{
    public Task<ArchiveOperationResult> UpdateAsync(string archivePath, IReadOnlyList<ArchiveChange> changes,
        string? password = null, ArchiveExtractionLimits? limits = null, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default, string? expectedHash = null) => Task.Run(async () =>
    {
        string archive = ArchivePathPolicy.LocalPath(archivePath);
        string type = ArchivePathPolicy.GetArchiveType(archive);
        if(type is not ("zip" or "7z") || ArchivePathPolicy.IsMultipart(archive))
            throw new NotSupportedException(ArchivePathPolicy.Text("ReadOnly"));
        if(changes.Count == 0) throw new ArgumentException(ArchivePathPolicy.Text("NoSelection"));
        limits ??= new ArchiveExtractionLimits();
        // No reader in this process may hold an old snapshot while replacement is committed.
        using var source = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        string originalHash = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken));
        if(expectedHash is not null && originalHash != expectedHash)
            throw new IOException(ArchivePathPolicy.Text("Changed"));
        var entries = await ReadEntriesAsync(archive, type, password, limits, cancellationToken);
        bool encrypted = entries.Any(entry => entry.IsEncrypted) || !string.IsNullOrEmpty(password);
        if(encrypted && string.IsNullOrEmpty(password)) throw new ArchivePasswordRequiredException();
        if(type == "zip" && encrypted && (password!.Length > 99 || password.Any(c => c < 32 || c > 126)))
            throw new NotSupportedException(ArchivePathPolicy.Text("ZipPasswordAscii"));
        string parent = Path.GetDirectoryName(archive)!;
        long additional = 0;
        foreach(var change in changes)
        {
            ArchivePathPolicy.ValidateEntry(change.Path);
            if(change.Kind == ArchiveChangeKind.Put)
            {
                string input = ArchivePathPolicy.LocalPath(change.Source!);
                await ValidateSourceTreeAsync(input, cancellationToken);
                additional = checked(additional + SourceBytes(input));
            }
            else if(change.Kind == ArchiveChangeKind.Rename) ArchivePathPolicy.ValidateEntry(change.Source!);
        }
        long total = checked(entries.Sum(entry => entry.Size) + additional);
        if(total > limits.MaximumBytes) throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
        long required = checked(total * 2 + source.Length + 32L * 1024 * 1024);
        if(new DriveInfo(Path.GetPathRoot(parent)!).AvailableFreeSpace < required)
            throw new IOException(ArchivePathPolicy.Text("NotEnoughSpace"));
        string workspace = ArchiveWorkspace.Create(parent, "archive-edit");
        bool preserveRecovery = false;
        try
        {
            string content = Path.Combine(workspace, "content");
            await ExtractAsync(archive, content, password, limits, progress, cancellationToken);
            var copyBudget = new CopyBudget(additional);
            foreach(var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string target = Path.Combine(content, ArchivePathPolicy.ValidateEntry(change.Path));
                ArchivePathPolicy.EnsureNoLinks(target);
                bool exists = File.Exists(target) || Directory.Exists(target);
                switch(change.Kind)
                {
                    case ArchiveChangeKind.Delete:
                        if(!exists) throw new FileNotFoundException(change.Path);
                        if(Directory.Exists(target)) Directory.Delete(target, true); else File.Delete(target);
                        break;
                    case ArchiveChangeKind.Rename:
                        if(!exists) throw new FileNotFoundException(change.Path);
                        string renamed = Path.Combine(content, ArchivePathPolicy.ValidateEntry(change.Source!));
                        if(target.Equals(renamed, StringComparison.OrdinalIgnoreCase) && !target.Equals(renamed, StringComparison.Ordinal))
                        {
                            string intermediate = Path.Combine(workspace, "renaming-" + Guid.NewGuid().ToString("N"));
                            if(Directory.Exists(target)) { Directory.Move(target, intermediate); Directory.Move(intermediate, renamed); }
                            else { File.Move(target, intermediate); File.Move(intermediate, renamed); }
                            break;
                        }
                        EnsureNewOutput(renamed);
                        if(ArchivePathPolicy.IsWithin(target, renamed)) throw new IOException(ArchivePathPolicy.Text("UnsafePath"));
                        Directory.CreateDirectory(Path.GetDirectoryName(renamed)!);
                        if(Directory.Exists(target)) Directory.Move(target, renamed); else File.Move(target, renamed);
                        break;
                    case ArchiveChangeKind.CreateDirectory:
                        EnsureNewOutput(target);
                        Directory.CreateDirectory(target);
                        break;
                    case ArchiveChangeKind.Put:
                        if(exists && !change.Overwrite) throw new IOException(ArchivePathPolicy.Text("OutputExists"));
                        // Folder merging would hide conflicts and is deliberately not implicit.
                        if(Directory.Exists(target)) throw new IOException(ArchivePathPolicy.Text("OutputExists"));
                        string input = ArchivePathPolicy.LocalPath(change.Source!);
                        if(Directory.Exists(input)) await CopyTreeAsync(input, target, copyBudget, cancellationToken);
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            await CopyFileAsync(input, target, change.Overwrite, copyBudget, cancellationToken);
                        }
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(changes));
                }
            }
            await ValidateSourceTreeAsync(content, cancellationToken);
            string[] paths = Directory.GetFileSystemEntries(content, "*", SearchOption.AllDirectories);
            if(paths.Length > limits.MaximumEntries || SourceBytes(content) > limits.MaximumBytes)
                throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
            string list = Path.Combine(workspace, "sources.txt");
            await File.WriteAllLinesAsync(list, Directory.GetFileSystemEntries(content)
                .Select(Path.GetFileName).Select(name => name!), new UTF8Encoding(false), cancellationToken);
            string temporary = Path.Combine(workspace, "updated." + type);
            List<string> args = ["a", "-t" + type, "-bso0", "-y", "-sse", "-snl", "-spd", "-scsUTF-8", "-i@" + list, temporary];
            if(encrypted) { args.Add("-p"); args.Add(type == "7z" ? "-mhe=on" : "-mem=AES256"); }
            progress?.Report(null, ArchivePathPolicy.Text("Rebuilding"));
            await engine.RunAsync(args, encrypted ? password : null, progress, cancellationToken, content);
            progress?.Report(null, ArchivePathPolicy.Text("Verifying"));
            await engine.RunAsync(["t", "-t" + type, "-bso0", temporary], encrypted ? password : null, progress, cancellationToken);
            var result = await ReadEntriesAsync(temporary, type, encrypted ? password : null, limits, cancellationToken);
            var expected = paths.Select(path => (Path: Path.GetRelativePath(content, path), Directory: Directory.Exists(path),
                Size: Directory.Exists(path) ? 0L : new FileInfo(path).Length)).OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
            var actual = result.Select(entry => (Path: entry.RelativePath, Directory: entry.IsDirectory, Size: entry.Size))
                .OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
            if(!expected.SequenceEqual(actual)) throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            // Verify the bytes too: a valid CRC alone cannot establish that the right input was rebuilt.
            using(var rebuilt = await OpenReadSessionAsync(temporary, encrypted ? password : null, cancellationToken))
            foreach(var entry in result.Where(entry => !entry.IsDirectory))
            {
                await using var input = rebuilt.OpenRead(entry, cancellationToken);
                await using var expectedInput = File.OpenRead(Path.Combine(content, entry.RelativePath));
                byte[] actualHash = await SHA256.HashDataAsync(input, cancellationToken);
                byte[] expectedContentHash = await SHA256.HashDataAsync(expectedInput, cancellationToken);
                if(!CryptographicOperations.FixedTimeEquals(actualHash, expectedContentHash))
                    throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            }
            using(var flush = new FileStream(temporary, FileMode.Open, FileAccess.Write, FileShare.None)) flush.Flush(true);
            cancellationToken.ThrowIfCancellationRequested();
            source.Dispose();
            // Reacquire a guard allowing our atomic replacement but excluding in-place writers.
            using(var guard = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                if(Convert.ToHexString(await SHA256.HashDataAsync(guard, cancellationToken)) != originalHash)
                    throw new IOException(ArchivePathPolicy.Text("SourceChanged"));
                ArchivePathPolicy.EnsureNoLinks(archive);
                cancellationToken.ThrowIfCancellationRequested();
                // No delete-then-move fallback. Failure leaves the old archive in place.
                string backup = Path.Combine(workspace, "original.backup");
                try
                {
                    File.Replace(temporary, archive, backup);
                    preserveRecovery = true;
                    // Windows replacement has no compare-and-swap API. A rename by another
                    // process in the final gap must never make its displaced data disposable.
                    using var displaced = File.OpenRead(backup);
                    if(Convert.ToHexString(await SHA256.HashDataAsync(displaced, CancellationToken.None)) != originalHash)
                        throw new IOException(ArchivePathPolicy.Text("SourceChanged"));
                    preserveRecovery = false;
                }
                catch(Exception exception) when(preserveRecovery || File.Exists(backup))
                {
                    preserveRecovery = true;
                    throw new IOException(Infrastructure.LocalizationManager.Format("Archive.CommitConflict", workspace), exception);
                }
            }
            progress?.Report(1, archive);
            return Result(archive, result);
        }
        finally { if(!preserveRecovery) await CleanupAsync(workspace); }
    }, cancellationToken);

    private static long SourceBytes(string path) => File.Exists(path) ? new FileInfo(path).Length :
        Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);

    private sealed class CopyBudget(long remaining)
    {
        internal long Remaining = remaining;
        internal int Entries;
    }

    private static async Task CopyFileAsync(string source, string destination, bool overwrite, CopyBudget budget, CancellationToken token)
    {
        ArchivePathPolicy.EnsureNoLinks(source);
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        if(++budget.Entries > 100_000 || input.Length > budget.Remaining)
            throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
        budget.Remaining -= input.Length;
        await using var output = new FileStream(destination, overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 81920, true);
        byte[] buffer = new byte[81920];
        long remaining = input.Length;
        while(remaining > 0)
        {
            int count = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
            if(count == 0) throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            if(new DriveInfo(Path.GetPathRoot(destination)!).AvailableFreeSpace < count + 16L * 1024 * 1024)
                throw new IOException(ArchivePathPolicy.Text("NotEnoughSpace"));
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            remaining -= count;
        }
        if(await input.ReadAsync(buffer.AsMemory(0, 1), token) != 0)
            throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
    }

    private static async Task CopyTreeAsync(string source, string destination, CopyBudget budget, CancellationToken token)
    {
        ArchivePathPolicy.EnsureNoLinks(source);
        if(++budget.Entries > 100_000) throw new IOException(ArchivePathPolicy.Text("TooManyEntries"));
        Directory.CreateDirectory(destination);
        foreach(string child in Directory.EnumerateFileSystemEntries(source))
        {
            token.ThrowIfCancellationRequested();
            ArchivePathPolicy.EnsureNoLinks(child);
            string target = Path.Combine(destination, Path.GetFileName(child));
            if(Directory.Exists(child)) await CopyTreeAsync(child, target, budget, token);
            else await CopyFileAsync(child, target, false, budget, token);
        }
    }
}

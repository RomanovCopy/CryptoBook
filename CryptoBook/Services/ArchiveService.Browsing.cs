using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;
using System.Text;

namespace CryptoBook.Services;

public sealed partial class ArchiveService
{
    internal Task<ArchiveReadSession> OpenReadSessionAsync(string path, string? password,
        CancellationToken token) => Task.Run(async () =>
    {
        string archive = ArchivePathPolicy.LocalPath(path);
        string type = ArchivePathPolicy.GetArchiveType(archive);
        var source = new ArchiveSourceLock(archive);
        try
        {
            IReadOnlyList<Entry> entries;
            try
            {
                entries = await ReadEntriesAsync(archive, type, password,
                    new ArchiveExtractionLimits(long.MaxValue), token);
            }
            catch(InvalidDataException exception) when(
                exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArchivePasswordRequiredException();
            }
            if(entries.Any(entry => entry.IsEncrypted) && string.IsNullOrEmpty(password))
                throw new ArchivePasswordRequiredException();
            // Validate a supplied password before showing an apparently usable session.
            // Reading a single encrypted entry avoids testing/decompressing the whole archive.
            var session = new ArchiveReadSession(this, source, archive, type, entries, password);
            try
            {
                Entry? encrypted = entries.FirstOrDefault(entry => entry.IsEncrypted && !entry.IsDirectory);
                if(encrypted is not null)
                {
                    await using Stream input = session.OpenRead(encrypted, token, passwordProbe: true);
                    if(await input.ReadAsync(new byte[1], token) == 0 && encrypted.Size != 0)
                        throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
                }
                return session;
            }
            catch(InvalidDataException exception) when(
                exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                session.Dispose();
                throw new ArchivePasswordRequiredException();
            }
            catch { session.Dispose(); throw; }
        }
        catch { source.Dispose(); throw; }
    }, token);

    internal async Task ReadEntryAsync(ArchiveReadSession session, Entry entry, Stream destination,
        CancellationToken token)
    {
        string workspace = ArchiveWorkspace.CreateTransient("archive-selection");
        string selection = Path.Combine(workspace, "selection.txt");
        bool sizeMismatch = false;
        try
        {
            await File.WriteAllLinesAsync(selection, [entry.Name], new UTF8Encoding(false), token);
            await engine.RunAsync(["x", "-t" + session.Type, "-so", "-bso0", "-y", "-spd", "-r-",
                "-scsUTF-8", "-i@" + selection, session.ArchivePath], session.GetPassword(), null, token,
                readOutput: async (input, cancellation) =>
                {
                    byte[] buffer = new byte[81920];
                    long remaining = entry.Size;
                    while(remaining > 0)
                    {
                        int read = await input.ReadAsync(buffer.AsMemory(0,
                            (int)Math.Min(buffer.Length, remaining)), cancellation);
                        if(read == 0) { sizeMismatch = true; return; }
                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellation);
                        remaining -= read;
                    }
                    if(await input.ReadAsync(buffer.AsMemory(0, 1), cancellation) != 0)
                        throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
                });
            if(sizeMismatch) throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
        }
        finally { await CleanupAsync(workspace); }
    }

    internal Task CopySelectionAsync(ArchiveReadSession session, string relative, string outputPath,
        IProgressReporter? progress, CancellationToken token) => Task.Run(async () =>
    {
        using var cancellation = session.CreateCancellation(token);
        token = cancellation.Token;
        string output = ArchivePathPolicy.LocalPath(outputPath);
        EnsureNewOutput(output);
        string parent = Path.GetDirectoryName(output)!;
        if(!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
        string prefix = relative.Length == 0 ? "" : relative + "\\";
        Entry[] entries = session.Entries.Where(entry => relative.Length == 0 ||
            entry.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase) ||
            entry.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        bool isFile = entries.Any(entry => !entry.IsDirectory &&
            entry.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));
        if(entries.Length == 0 && relative.Length != 0) throw new FileNotFoundException(relative);
        long total = entries.Sum(entry => entry.Size);
        if(total > new ArchiveExtractionLimits().MaximumBytes)
            throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
        if(new DriveInfo(Path.GetPathRoot(parent)!).AvailableFreeSpace - 16L * 1024 * 1024 < total)
            throw new IOException(ArchivePathPolicy.Text("NotEnoughSpace"));
        string staging = ArchiveWorkspace.Create(parent, "extract");
        string result = Path.Combine(staging, "result");
        if(!isFile) Directory.CreateDirectory(result);
        long completed = 0;
        try
        {
            foreach(Entry entry in entries)
            {
                token.ThrowIfCancellationRequested();
                string suffix = relative.Length == 0 ? entry.RelativePath :
                    entry.RelativePath.Length == relative.Length ? "" : entry.RelativePath[prefix.Length..];
                string target = isFile ? result : suffix.Length == 0 ? result : Path.Combine(result, suffix);
                ArchivePathPolicy.EnsureNoLinks(target);
                if(entry.IsDirectory) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using(var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 81920, FileOptions.Asynchronous))
                await using(var input = session.OpenRead(entry, token))
                {
                    byte[] buffer = new byte[81920];
                    int count;
                    while((count = await input.ReadAsync(buffer, token)) > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, count), token);
                        completed += count;
                        progress?.Report(total == 0 ? 1 : completed / (double)total, entry.Name);
                    }
                    await destination.FlushAsync(token);
                }
                if(entry.ModifiedUtc is DateTime modified) File.SetLastWriteTimeUtc(target, modified);
            }
            await RetryFileSystemAsync(() =>
            {
                ArchivePathPolicy.EnsureNoLinks(parent);
                if(isFile) File.Move(result, output);
                else Directory.Move(result, output);
            }, token);
            progress?.Report(1, output);
        }
        finally { await CleanupAsync(staging); }
    }, token);
}

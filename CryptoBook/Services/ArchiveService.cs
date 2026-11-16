using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Globalization;
using System.IO;
using System.Text;

namespace CryptoBook.Services;

/// <summary>
/// Local archive operations. Native extraction writes only to stdout; this service
/// owns every destination path and never delegates filesystem extraction to 7-Zip.
/// </summary>
public sealed class ArchiveService: IArchiveService
{
    private readonly SevenZipArchiveEngine engine;

    public ArchiveService() : this(new SevenZipArchiveEngine()) { }
    internal ArchiveService(SevenZipArchiveEngine engine) => this.engine = engine;

    public Task<ArchiveOperationResult> CreateAsync(IReadOnlyList<string> sources, string outputPath,
        ArchiveFormat format, string? password = null, IProgressReporter? progress = null,
        CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(sources.Count == 0) throw new ArgumentException(ArchivePathPolicy.Text("NoSelection"));
        if(format == ArchiveFormat.Zip && !string.IsNullOrEmpty(password) &&
           (password.Any(character => character < 32 || character > 126) || password.Length > 99))
            throw new ArgumentException(ArchivePathPolicy.Text("ZipPasswordAscii"));
        string output = ArchivePathPolicy.LocalPath(outputPath);
        string type = format switch
        {
            ArchiveFormat.Zip => "zip", ArchiveFormat.SevenZip => "7z",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        if(!Path.GetExtension(output).Equals("." + type, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(ArchivePathPolicy.Text("ExtensionMismatch"));
        EnsureNewOutput(output);
        string parent = Path.GetDirectoryName(output)!;
        if(!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
        string[] inputs = sources.Select(ArchivePathPolicy.LocalPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        // Selecting a directory already includes all selected descendants.
        inputs = inputs.Where(path => !inputs.Any(other =>
            !other.Equals(path, StringComparison.OrdinalIgnoreCase) && Directory.Exists(other) &&
            ArchivePathPolicy.IsWithin(other, path))).ToArray();
        string common = Path.GetDirectoryName(inputs[0]) ??
            throw new ArgumentException(ArchivePathPolicy.Text("RootUnsupported"));
        foreach(string input in inputs)
        {
            if(Path.GetPathRoot(input)!.Equals(input, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(ArchivePathPolicy.Text("RootUnsupported"));
            while(!ArchivePathPolicy.IsWithin(common, input))
                common = Path.GetDirectoryName(common) ??
                    throw new ArgumentException(ArchivePathPolicy.Text("DifferentDrives"));
            if(Directory.Exists(input) && ArchivePathPolicy.IsWithin(input, output))
                throw new ArgumentException(ArchivePathPolicy.Text("OutputInsideSource"));
            await ValidateSourceTreeAsync(input, cancellationToken);
        }
        string workspace = Path.Combine(parent, ".cryptobook-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            string list = Path.Combine(workspace, "sources.txt");
            string[] names = inputs.Select(path => ArchivePathPolicy.ValidateEntry(Path.GetRelativePath(common, path))).ToArray();
            await File.WriteAllLinesAsync(list, names, new UTF8Encoding(false), cancellationToken);
            string temporary = Path.Combine(workspace, "archive." + type);
            var arguments = new List<string> { "a", "-t" + type, "-bso0", "-y", "-sse", "-snl", "-spd", "-scsUTF-8",
                "-i@" + list, temporary };
            if(!string.IsNullOrEmpty(password))
            {
                arguments.Add("-p");
                arguments.Add(type == "7z" ? "-mhe=on" : "-mem=AES256");
            }
            progress?.Report(null, ArchivePathPolicy.Text("Creating"));
            await engine.RunAsync(arguments, password, progress, cancellationToken, common);
            progress?.Report(null, ArchivePathPolicy.Text("Verifying"));
            // Exit code 1 (warnings, skipped/unreadable input) is a failure too.
            await engine.RunAsync(["t", "-t" + type, "-bso0", temporary], password, progress, cancellationToken);
            IReadOnlyList<Entry> entries = await ReadEntriesAsync(temporary, type, password,
                new ArchiveExtractionLimits(long.MaxValue), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await RetryFileSystemAsync(() =>
            {
                ArchivePathPolicy.EnsureNoLinks(parent);
                File.Move(temporary, output); // Exclusive publication; never replace an existing file.
            }, cancellationToken);
            progress?.Report(1, output);
            return Result(output, entries);
        }
        finally { await CleanupAsync(workspace); }
    }, cancellationToken);

    public Task<ArchiveOperationResult> ExtractAsync(string archivePath, string outputDirectory,
        string? password = null, ArchiveExtractionLimits? limits = null,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        string archive = ArchivePathPolicy.LocalPath(archivePath);
        string output = ArchivePathPolicy.LocalPath(outputDirectory);
        string type = ArchivePathPolicy.GetArchiveType(archive);
        limits ??= new ArchiveExtractionLimits();
        if(limits.MaximumBytes < 1 || limits.MaximumEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(limits));
        EnsureNewOutput(output);
        string parent = Path.GetDirectoryName(output)!;
        if(!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
        // Keep the source stable while listing and extracting (7-Zip opens it read-only).
        using var sourceLock = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        progress?.Report(null, ArchivePathPolicy.Text("Reading"));
        IReadOnlyList<Entry> entries = await ReadEntriesAsync(archive, type, password, limits, cancellationToken);
        long total = entries.Sum(entry => entry.Size);
        if(new DriveInfo(Path.GetPathRoot(parent)!).AvailableFreeSpace - 16L * 1024 * 1024 < total)
            throw new IOException(ArchivePathPolicy.Text("NotEnoughSpace"));
        string staging = Path.Combine(parent, ".cryptobook-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        long completed = 0;
        try
        {
            // Both technical listing and extraction enumerate archive item indices in order.
            // A single stdout stream avoids repeatedly decompressing solid blocks.
            await engine.RunAsync(["x", "-t" + type, "-so", "-bso0", "-y", archive], password, null,
                cancellationToken, readOutput: async (stream, token) =>
            {
                byte[] buffer = new byte[81920];
                foreach(Entry entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    string target = Path.GetFullPath(Path.Combine(staging, entry.RelativePath));
                    if(!ArchivePathPolicy.IsWithin(staging, target))
                        throw new InvalidDataException(ArchivePathPolicy.Text("UnsafePath"));
                    ArchivePathPolicy.EnsureNoLinks(target);
                    if(entry.IsDirectory) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using(var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, buffer.Length, FileOptions.Asynchronous))
                    {
                        long written = 0;
                        while(written < entry.Size)
                        {
                            int count = await stream.ReadAsync(buffer.AsMemory(0,
                                (int)Math.Min(buffer.Length, entry.Size - written)), token);
                            if(count == 0)
                                throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
                            await destination.WriteAsync(buffer.AsMemory(0, count), token);
                            written += count;
                            progress?.Report(total == 0 ? null : (completed + written) / (double)total, entry.Name);
                        }
                        await destination.FlushAsync(token);
                    }
                    completed += entry.Size;
                    if(entry.ModifiedUtc is DateTime modified) File.SetLastWriteTimeUtc(target, modified);
                }
                if(await stream.ReadAsync(buffer.AsMemory(0, 1), token) != 0)
                    throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            });
            cancellationToken.ThrowIfCancellationRequested();
            await RetryFileSystemAsync(() =>
            {
                ArchivePathPolicy.EnsureNoLinks(parent);
                Directory.Move(staging, output); // The requested folder must still be absent.
            }, cancellationToken);
            progress?.Report(1, output);
            return Result(output, entries);
        }
        finally { await CleanupAsync(staging); }
    }, cancellationToken);

    public Task<ArchiveOperationResult> TestAsync(string archivePath, string? password = null,
        IProgressReporter? progress = null, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        string archive = ArchivePathPolicy.LocalPath(archivePath);
        string type = ArchivePathPolicy.GetArchiveType(archive);
        using var sourceLock = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        progress?.Report(null, ArchivePathPolicy.Text("Verifying"));
        await engine.RunAsync(["t", "-t" + type, "-bso0", archive], password, progress, cancellationToken);
        progress?.Report(1, ArchivePathPolicy.Text("TestSuccess"));
        return new ArchiveOperationResult(archive, 0, 0);
    }, cancellationToken);

    private async Task<IReadOnlyList<Entry>> ReadEntriesAsync(string archive, string type, string? password,
        ArchiveExtractionLimits limits, CancellationToken token)
    {
        string listing = await engine.RunAsync(["l", "-t" + type, "-slt", "-ba", "-bso0", archive],
            password, null, token);
        IReadOnlyList<Entry> entries = ParseEntries(listing, limits);
        // Console listings replace CR/LF in names with underscores. Verify such
        // names by literal native selection so they cannot silently be renamed.
        if(entries.Any(entry => entry.Name.Contains('_')))
        {
            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(Entry entry in entries)
                for(string? parent = Path.GetDirectoryName(entry.RelativePath); !string.IsNullOrEmpty(parent);
                    parent = Path.GetDirectoryName(parent)) parents.Add(parent);
            Entry[] leaves = entries.Where(entry => !entry.IsDirectory || !parents.Contains(entry.RelativePath)).ToArray();
            string selection = Path.GetTempFileName();
            try
            {
                await File.WriteAllLinesAsync(selection, leaves.Select(entry => entry.Name), new UTF8Encoding(false), token);
                string filtered = await engine.RunAsync(["l", "-t" + type, "-slt", "-ba", "-bso0", "-spd", "-r-",
                    "-scsUTF-8", "-i@" + selection, archive], password, null, token);
                IReadOnlyList<Entry> actual = ParseEntries(filtered, limits);
                if(!leaves.Select(entry => (entry.Name, entry.IsDirectory, entry.Size)).SequenceEqual(
                    actual.Select(entry => (entry.Name, entry.IsDirectory, entry.Size))))
                    throw new InvalidDataException(ArchivePathPolicy.Text("UnsafePath"));
            }
            finally { File.Delete(selection); }
        }
        return entries;
    }

    internal static IReadOnlyList<Entry> ParseEntries(string listing, ArchiveExtractionLimits limits)
    {
        var entries = new List<Entry>();
        var identities = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var explicitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        long total = 0;
        using var reader = new StringReader(listing);
        string? line;
        while((line = reader.ReadLine()) is not null)
        {
            if(line.Length == 0) { CompleteEntry(); continue; }
            // 7-Zip's list command always sends this prompt to stdout, even with -bso0.
            if(fields.Count == 0 && entries.Count == 0 &&
               line == "Enter password (will not be echoed):") continue;
            int separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if(separator < 1 || !fields.TryAdd(line[..separator], line[(separator + 3)..]))
                throw new InvalidDataException(ArchivePathPolicy.Text("UnsafePath"));
        }
        CompleteEntry();
        return entries;

        void CompleteEntry()
        {
            if(fields.Count == 0) return;
            if(!fields.TryGetValue("Path", out string? name) ||
               !fields.TryGetValue("Size", out string? sizeText) ||
               !long.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out long size))
                throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            string relative = ArchivePathPolicy.ValidateEntry(name.TrimEnd('/', '\\'));
            string attributes = fields.GetValueOrDefault("Attributes", "");
            if(fields.Any(field => (field.Key.Contains("Link", StringComparison.OrdinalIgnoreCase) ||
                    field.Key.Contains("Reparse", StringComparison.OrdinalIgnoreCase)) && field.Value is not "" and not "-") ||
               attributes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(part => part.StartsWith('l')) ||
               fields.GetValueOrDefault("Anti") == "+")
                throw new InvalidDataException(ArchivePathPolicy.Text("LinksUnsupported"));
            bool directory = fields.GetValueOrDefault("Folder") == "+" || attributes.StartsWith('D');
            if(directory && size != 0) throw new InvalidDataException(ArchivePathPolicy.Text("SizeMismatch"));
            if(!explicitNames.Add(relative)) throw new InvalidDataException(ArchivePathPolicy.Text("DuplicatePath"));
            if(identities.TryGetValue(relative, out bool wasDirectory) && (!directory || !wasDirectory))
                throw new InvalidDataException(ArchivePathPolicy.Text("DuplicatePath"));
            identities[relative] = directory;
            for(string? parent = Path.GetDirectoryName(relative); !string.IsNullOrEmpty(parent);
                parent = Path.GetDirectoryName(parent))
            {
                if(identities.TryGetValue(parent, out bool isDirectory) && !isDirectory)
                    throw new InvalidDataException(ArchivePathPolicy.Text("DuplicatePath"));
                identities[parent] = true;
            }
            if(entries.Count >= limits.MaximumEntries)
                throw new InvalidDataException(ArchivePathPolicy.Text("TooManyEntries"));
            if(size > limits.MaximumBytes - total)
                throw new InvalidDataException(ArchivePathPolicy.Text("ExtractionLimit"));
            total += size;
            DateTime? modified = DateTime.TryParse(fields.GetValueOrDefault("Modified"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out DateTime date) ? date.ToUniversalTime() : null;
            entries.Add(new Entry(name, relative, directory, size, modified));
            fields.Clear();
        }
    }

    private static Task ValidateSourceTreeAsync(string root, CancellationToken token)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        int count = 0;
        while(pending.TryPop(out string? path))
        {
            token.ThrowIfCancellationRequested();
            ArchivePathPolicy.EnsureNoLinks(path);
            FileAttributes attributes = File.GetAttributes(path);
            ArchivePathPolicy.ValidateEntry(Path.GetFileName(path));
            if(++count > 100_000) throw new IOException(ArchivePathPolicy.Text("TooManyEntries"));
            if((attributes & FileAttributes.Directory) != 0)
                foreach(string child in Directory.EnumerateFileSystemEntries(path)) pending.Push(child);
        }
        return Task.CompletedTask;
    }

    private static void EnsureNewOutput(string path)
    {
        if(File.Exists(path) || Directory.Exists(path))
            throw new IOException(ArchivePathPolicy.Text("OutputExists"));
    }

    private static ArchiveOperationResult Result(string path, IReadOnlyList<Entry> entries) =>
        new(path, entries.Count(entry => !entry.IsDirectory), entries.Sum(entry => entry.Size));

    private static async Task CleanupAsync(string path)
    {
        // Only generated operation directories are passed here. Never remove the destination.
        try
        {
            await RetryFileSystemAsync(() =>
            {
                if(Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }, CancellationToken.None);
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(Infrastructure.LocalizationManager.Format("Archive.CleanupFailed", path), exception);
        }
    }

    private static async Task RetryFileSystemAsync(Action action, CancellationToken token)
    {
        for(int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { action(); return; }
            catch(Exception exception) when(attempt < 4 && exception is IOException or UnauthorizedAccessException &&
                (exception.HResult & 0xFFFF) is 5 or 32 or 33)
            {
                // Indexers/antivirus may briefly hold a newly written file or directory.
                await Task.Delay(50 << attempt, token);
            }
        }
    }

    internal sealed record Entry(string Name, string RelativePath, bool IsDirectory, long Size, DateTime? ModifiedUtc);
}

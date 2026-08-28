using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

public sealed class LocalFileSystemFacade: ILocalFileSystemFacade
{
    public string? GetParent(string path) => Path.GetDirectoryName(path);
    public string Combine(params string[] parts) => Path.Combine(parts);
    public string GetName(string path) => Path.GetFileName(path);
    public string GetNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
    public string GetExtension(string path) => Path.GetExtension(path);
    public string ChangeExtension(string path, string? extension) =>
        Path.ChangeExtension(path, extension);
    public string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public string TemporaryPath(params string[] parts) =>
        Path.Combine([Path.GetTempPath(), .. parts]);
    public bool Exists(string path) => Path.Exists(path);
    public bool FileExists(string path) => File.Exists(path);

    public bool IsValidFileName(string name) =>
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !name.Contains(Path.DirectorySeparatorChar) &&
        !name.Contains(Path.AltDirectorySeparatorChar);

    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    public IReadOnlyList<string> EnumerateDirectories(string path) =>
        Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();

    public void DeleteDirectoryIfExists(string path)
    {
        try
        {
            if(Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch(IOException)
        {
        }
        catch(UnauthorizedAccessException)
        {
        }
    }

    public void DeleteFileIfExists(string path)
    {
        try
        {
            if(File.Exists(path))
                File.Delete(path);
        }
        catch(IOException)
        {
        }
        catch(UnauthorizedAccessException)
        {
        }
    }

    public async Task CopyFileAtomicallyAsync(
        string sourcePath,
        string destinationPath,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if(string.IsNullOrWhiteSpace(destinationDirectory))
            throw new IOException($"Cannot determine destination directory for '{destinationPath}'.");
        Directory.CreateDirectory(destinationDirectory);

        string stagingPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using FileStream input = new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using(FileStream output = new(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[81920];
                long copied = 0;
                int read;
                while((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    copied += read;
                    progress?.Report(
                        input.Length == 0 ? 1 : (double)copied / input.Length,
                        destinationPath);
                }
                await output.FlushAsync(cancellationToken);
            }
            AtomicFileCommit.CommitWithoutBackup(stagingPath, destinationPath);
            progress?.Report(1, destinationPath);
        }
        finally
        {
            DeleteFileIfExists(stagingPath);
        }
    }
}

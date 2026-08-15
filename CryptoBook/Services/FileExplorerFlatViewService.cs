using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

public sealed class FileExplorerFlatViewService:
    IFileExplorerFlatViewService
{
    private readonly IFileManagerService fileManagerService;
    private readonly object monitorGate = new();
    private FileSystemWatcher? watcher;
    private bool disposed;

    public event EventHandler? FilesChanged;

    public FileExplorerFlatViewService(IFileManagerService fileManagerService)
    {
        this.fileManagerService = fileManagerService ??
            throw new ArgumentNullException(nameof(fileManagerService));
    }

    public async Task<FileExplorerFlatScanResult> ScanAsync(
        string rootPath,
        bool includeHidden,
        CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path is required.", nameof(rootPath));

        string normalizedRoot = NormalizePath(rootPath);
        var files = new List<IFileItem>();
        var pendingDirectories = new Stack<string>();
        var visitedDirectories = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        int skippedDirectoryCount = 0;

        pendingDirectories.Push(normalizedRoot);
        while(pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pendingDirectories.Pop();
            if(!visitedDirectories.Add(NormalizePath(directory)))
                continue;

            List<ISystemItem> children;
            try
            {
                children = await fileManagerService.BrowseAsync(
                    directory,
                    null,
                    cancellationToken,
                    includeHidden);
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch(IOException) when(!PathsEqual(directory, normalizedRoot))
            {
                skippedDirectoryCount++;
                continue;
            }

            files.AddRange(children.OfType<IFileItem>());
            foreach(IContainerSystemItem childDirectory in
                children.OfType<IContainerSystemItem>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(ShouldSkipDirectory(childDirectory.FullPath))
                {
                    skippedDirectoryCount++;
                    continue;
                }

                pendingDirectories.Push(childDirectory.FullPath);
            }
        }

        IFileItem[] sortedFiles = files
            .OrderBy(
                file => string.Concat(file.Name, file.Extension),
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                file => file.FullPath,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new FileExplorerFlatScanResult(
            sortedFiles,
            skippedDirectoryCount);
    }

    public void StartMonitoring(string rootPath)
    {
        if(string.IsNullOrWhiteSpace(rootPath))
            return;

        string normalizedRoot = NormalizePath(rootPath);
        lock(monitorGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(watcher is not null &&
               PathsEqual(watcher.Path, normalizedRoot))
            {
                return;
            }

            StopMonitoringCore();
            if(!Directory.Exists(normalizedRoot))
                return;

            FileSystemWatcher? nextWatcher = null;
            try
            {
                nextWatcher = new FileSystemWatcher(normalizedRoot)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName |
                        NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite |
                        NotifyFilters.Size |
                        NotifyFilters.Attributes,
                    EnableRaisingEvents = false
                };
                nextWatcher.Created += Watcher_Changed;
                nextWatcher.Deleted += Watcher_Changed;
                nextWatcher.Changed += Watcher_Changed;
                nextWatcher.Renamed += Watcher_Renamed;
                nextWatcher.Error += Watcher_Error;
                nextWatcher.EnableRaisingEvents = true;
                watcher = nextWatcher;
            }
            catch(Exception exception) when(
                exception is ArgumentException or IOException or
                UnauthorizedAccessException or PlatformNotSupportedException)
            {
                nextWatcher?.Dispose();
            }
        }
    }

    public void StopMonitoring()
    {
        lock(monitorGate)
            StopMonitoringCore();
    }

    public void Dispose()
    {
        lock(monitorGate)
        {
            if(disposed)
                return;

            disposed = true;
            StopMonitoringCore();
        }
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e) =>
        FilesChanged?.Invoke(this, EventArgs.Empty);

    private void Watcher_Renamed(object sender, RenamedEventArgs e) =>
        FilesChanged?.Invoke(this, EventArgs.Empty);

    private void Watcher_Error(object sender, ErrorEventArgs e) =>
        FilesChanged?.Invoke(this, EventArgs.Empty);

    private void StopMonitoringCore()
    {
        if(watcher is null)
            return;

        try
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= Watcher_Changed;
            watcher.Deleted -= Watcher_Changed;
            watcher.Changed -= Watcher_Changed;
            watcher.Renamed -= Watcher_Renamed;
            watcher.Error -= Watcher_Error;
            watcher.Dispose();
        }
        finally
        {
            watcher = null;
        }
    }

    private static bool ShouldSkipDirectory(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch(UnauthorizedAccessException)
        {
            return true;
        }
        catch(IOException)
        {
            return true;
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}

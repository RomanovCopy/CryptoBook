using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Xml.Linq;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerFlatViewTests
{
    [Fact]
    public async Task ScanAsync_ReturnsOnlyFilesFromEntireDirectoryTree()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string firstDirectory = Directory.CreateDirectory(
            Path.Combine(temporaryDirectory.Path, "first")).FullName;
        string secondDirectory = Directory.CreateDirectory(
            Path.Combine(firstDirectory, "second")).FullName;
        string rootFile = Path.Combine(temporaryDirectory.Path, "root.txt");
        string nestedFile = Path.Combine(firstDirectory, "nested.md");
        string deepFile = Path.Combine(secondDirectory, "deep.cbook");
        await File.WriteAllTextAsync(rootFile, "root");
        await File.WriteAllTextAsync(nestedFile, "nested");
        await File.WriteAllTextAsync(deepFile, "deep");

        using var service = new FileExplorerFlatViewService(
            new LocalBrowseFileManager());

        var result = await service.ScanAsync(
            temporaryDirectory.Path,
            includeHidden: false);

        Assert.Equal(3, result.Files.Count);
        Assert.True(
            new HashSet<string>(
                result.Files.Select(file => file.FullPath),
                StringComparer.OrdinalIgnoreCase)
            .SetEquals([rootFile, nestedFile, deepFile]));
        Assert.All(result.Files, file => Assert.NotNull(file.Parent));
        Assert.Equal(0, result.SkippedDirectoryCount);
    }

    [Fact]
    public async Task ScanAsync_ObservesHiddenFileSettingAndCancellation()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string visibleFile = Path.Combine(temporaryDirectory.Path, "visible.txt");
        string hiddenFile = Path.Combine(temporaryDirectory.Path, "hidden.txt");
        await File.WriteAllTextAsync(visibleFile, "visible");
        await File.WriteAllTextAsync(hiddenFile, "hidden");
        File.SetAttributes(
            hiddenFile,
            File.GetAttributes(hiddenFile) | FileAttributes.Hidden);

        using var service = new FileExplorerFlatViewService(
            new LocalBrowseFileManager());

        var withoutHidden = await service.ScanAsync(
            temporaryDirectory.Path,
            includeHidden: false);
        var withHidden = await service.ScanAsync(
            temporaryDirectory.Path,
            includeHidden: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Equal(
            [visibleFile],
            withoutHidden.Files.Select(file => file.FullPath),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, withHidden.Files.Count);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ScanAsync(
                temporaryDirectory.Path,
                includeHidden: true,
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Monitoring_RaisesChangeForNestedFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string nestedDirectory = Directory.CreateDirectory(
            Path.Combine(temporaryDirectory.Path, "nested")).FullName;
        using var service = new FileExplorerFlatViewService(
            new LocalBrowseFileManager());
        var changed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        service.FilesChanged += (_, _) => changed.TrySetResult();
        service.StartMonitoring(temporaryDirectory.Path);

        await File.WriteAllTextAsync(
            Path.Combine(nestedDirectory, "created.txt"),
            "created");

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.StopMonitoring();
    }

    [Fact]
    public void ItemFilter_InFlatViewMatchesRelativeDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "CryptoBook.FlatRoot");
        var file = new FileItem
        {
            Name = "report",
            Extension = ".txt",
            FullPath = Path.Combine(root, "Archive", "2026", "report.txt")
        };

        Assert.True(FileExplorerItemFilter.Matches(file, "archive", root));
        Assert.True(FileExplorerItemFilter.Matches(file, "2026", root));
        Assert.False(FileExplorerItemFilter.Matches(file, "drafts", root));
    }

    [Fact]
    public void FileExplorerXaml_ExposesFlatViewToggleAndFolderColumn()
    {
        string xamlPath = FindRepositoryFile(
            "CryptoBook",
            "Views",
            "FileExplorer.xaml");
        XDocument document = XDocument.Load(xamlPath);
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        string xaml = document.ToString(SaveOptions.DisableFormatting);

        IEnumerable<XElement> gridViews = document.Descendants(
            presentation + "GridView");
        XElement standardView = Assert.Single(
            gridViews,
            element =>
                (string?)element.Attribute(x + "Key") ==
                "FileExplorerStandardGridView");
        XElement flatView = Assert.Single(
            gridViews,
            element =>
                (string?)element.Attribute(x + "Key") ==
                "FileExplorerFlatGridView");
        string[] standardHeaders = standardView
            .Descendants(presentation + "GridViewColumnHeader")
            .Select(header => (string?)header.Attribute("Tag"))
            .OfType<string>()
            .ToArray();
        string[] flatHeaders = flatView
            .Descendants(presentation + "GridViewColumnHeader")
            .Select(header => (string?)header.Attribute("Tag"))
            .OfType<string>()
            .ToArray();

        Assert.Contains("IsFlatViewEnabled", xaml);
        Assert.Contains("Explorer.FlatView.Toggle", xaml);
        Assert.Contains("Explorer.Column.Folder", xaml);
        Assert.Contains("RelativeDirectoryConverter", xaml);
        Assert.Contains("CancelFlatViewScanCommand", xaml);
        Assert.Equal(
            ["Name", "LastWriteTimeUtc", "Extension", "Size"],
            standardHeaders);
        Assert.Equal(
            [
                "Name",
                "RelativeDirectory",
                "LastWriteTimeUtc",
                "Extension",
                "Size"
            ],
            flatHeaders);
        Assert.DoesNotContain("RelativeDirectory", standardHeaders);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                Path.Combine(parts));
            if(File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(parts));
    }

    private sealed class LocalBrowseFileManager: IFileManagerService
    {
        public Task<List<ISystemItem>> BrowseAsync(
            string path,
            IProgressReporter? progress = null,
            CancellationToken ct = default,
            bool includeHidden = false)
        {
            ct.ThrowIfCancellationRequested();
            var parent = new TestDirectoryItem(path, null);
            var items = new List<ISystemItem>();
            foreach(string directory in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                if(includeHidden || !IsHidden(directory))
                    items.Add(new TestDirectoryItem(directory, parent));
            }
            foreach(string filePath in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                if(!includeHidden && IsHidden(filePath))
                    continue;

                var info = new FileInfo(filePath);
                items.Add(new FileItem
                {
                    Name = Path.GetFileNameWithoutExtension(info.Name),
                    Extension = info.Extension,
                    FullPath = info.FullName,
                    RootDirectory = info.Directory?.Root.FullName ??
                        string.Empty,
                    Parent = parent,
                    Size = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc
                });
            }

            return Task.FromResult(items);
        }

        private static bool IsHidden(string path) =>
            (File.GetAttributes(path) & FileAttributes.Hidden) != 0;

        public Task<FileOperationResult> CopyAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> MoveAsync(
            string sourcePath,
            string destinationPath,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> DeleteAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> RenameAsync(
            string path,
            string newName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> CreateDirectoryAsync(
            string parentDirectory,
            string newDirectoryName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> CanReadAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> CanWriteAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(
            string path,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Stream> OpenWriteAsync(
            string path,
            bool overwrite,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> IsHiddenAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> SetHiddenAsync(
            string path,
            bool hidden,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> IsReadOnlyAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> SetReadOnlyAsync(
            string path,
            bool isReadOnly,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
        public string NormalizePath(string rawPath) =>
            Path.GetFullPath(rawPath);
    }

    private sealed class TestDirectoryItem: IContainerSystemItem
    {
        private readonly ReadOnlyObservableCollection<ISystemItem> children =
            new(new ObservableCollection<ISystemItem>());
        private readonly ReadOnlyObservableCollection<IContainerSystemItem>
            directoryChildren =
                new(new ObservableCollection<IContainerSystemItem>());

        public TestDirectoryItem(string path, ISystemItem? parent)
        {
            var info = new DirectoryInfo(path);
            Name = info.Name;
            FullPath = info.FullName;
            RootDirectory = info.Root.FullName;
            Parent = parent;
            LastWriteTimeUtc = info.LastWriteTimeUtc;
        }

        public string Name { get; set; }
        public string FullPath { get; set; }
        public string RootDirectory { get; set; }
        public long Size { get; set; }
        public bool IsEditing { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public ISystemItem? Parent { get; set; }
        public bool IsLoaded { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }
        public ReadOnlyObservableCollection<ISystemItem> Children => children;
        public ReadOnlyObservableCollection<IContainerSystemItem>
            DirectoryChildren => directoryChildren;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public Task<FileOperationResult> AddChildAsync(
            IEnumerable<ISystemItem> items,
            Func<ISystemItem, string> keySelector,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> RenameChildAsync(
            ISystemItem item,
            string newName,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> RemoveChildAsync(
            IEnumerable<ISystemItem> items,
            Func<ISystemItem, string> keySelector,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> SortingAsync(
            SystemItemSortType sortType,
            int dir = 0,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<FileOperationResult> ClearChildrenAsync() =>
            throw new NotSupportedException();
        public Task SyncCollectionsAsync(
            IEnumerable<ISystemItem> source,
            Func<ISystemItem, string> keySelector,
            Action<ISystemItem, ISystemItem>? updateExisting,
            CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory: IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "CryptoBook.FlatViewTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if(!Directory.Exists(Path))
                return;

            foreach(string file in Directory.EnumerateFiles(
                Path,
                "*",
                SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(Path, recursive: true);
        }
    }
}

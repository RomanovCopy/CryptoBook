using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerSelectionPolicyTests
{
    [Fact]
    public void CreateSnapshot_IsReadOnlyAndDetachedFromLiveSelection()
    {
        var source = new List<ISystemItem>
        {
            CreateFile(@"C:\work\one.txt")
        };

        IReadOnlyList<ISystemItem> snapshot =
            FileExplorerSelectionPolicy.CreateSnapshot(source);
        source.Add(CreateFile(@"C:\work\two.txt"));

        Assert.Single(snapshot);
        Assert.Throws<NotSupportedException>(() =>
            ((IList)snapshot).Add(CreateFile(@"C:\work\three.txt")));
    }

    [Fact]
    public void NormalizeForOperation_RemovesDuplicatesAndNestedSelections()
    {
        var directory = new ContainerStub(@"C:\work\folder");
        var nestedFile = CreateFile(@"C:\work\folder\nested.txt");
        var siblingFile = CreateFile(@"C:\work\sibling.txt");

        IReadOnlyList<ISystemItem> result =
            FileExplorerSelectionPolicy.NormalizeForOperation(new ISystemItem[]
            {
                directory,
                nestedFile,
                siblingFile,
                siblingFile
            });

        Assert.Equal(2, result.Count);
        Assert.Same(directory, result[0]);
        Assert.Same(siblingFile, result[1]);
    }

    [Fact]
    public void ContainsDrive_RejectsWholeDriveSelection()
    {
        Assert.True(FileExplorerSelectionPolicy.ContainsDrive(
            new ISystemItem[] { new DriveStub(@"C:\") }));
        Assert.False(FileExplorerSelectionPolicy.ContainsDrive(
            new ISystemItem[] { CreateFile(@"C:\work\one.txt") }));
    }

    private static FileItem CreateFile(string path) => new()
    {
        FullPath = path,
        Name = Path.GetFileName(path),
        RootDirectory = Path.GetPathRoot(path) ?? string.Empty
    };

    private class ContainerStub: IContainerSystemItem
    {
        private readonly ReadOnlyObservableCollection<ISystemItem> children =
            new(new ObservableCollection<ISystemItem>());
        private readonly ReadOnlyObservableCollection<IContainerSystemItem> directories =
            new(new ObservableCollection<IContainerSystemItem>());

        public ContainerStub(string path)
        {
            FullPath = path;
            Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            RootDirectory = Path.GetPathRoot(path) ?? string.Empty;
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
        public ReadOnlyObservableCollection<IContainerSystemItem> DirectoryChildren => directories;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public Task<FileOperationResult> AddChildAsync(
            IEnumerable<ISystemItem> items,
            Func<ISystemItem, string> keySelector,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> RenameChildAsync(
            ISystemItem item,
            string newName,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> RemoveChildAsync(
            IEnumerable<ISystemItem> items,
            Func<ISystemItem, string> keySelector,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> SortingAsync(
            SystemItemSortType sortType,
            int dir = 0,
            CancellationToken ct = default) =>
            Task.FromResult(FileOperationResult.Ok());

        public Task<FileOperationResult> ClearChildrenAsync() =>
            Task.FromResult(FileOperationResult.Ok());

        public Task SyncCollectionsAsync(
            IEnumerable<ISystemItem> source,
            Func<ISystemItem, string> keySelector,
            Action<ISystemItem, ISystemItem>? updateExisting,
            CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class DriveStub: ContainerStub, IDriveItem
    {
        public DriveStub(string path): base(path)
        {
        }

        public string VolumeLabel { get; set; } = string.Empty;
        public string DriveFormat { get; set; } = string.Empty;
        public System.IO.DriveType DriveType { get; set; }
        public long AvailableFreeSpace { get; set; }
        public long TotalSize { get; set; }
    }
}

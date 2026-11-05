using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Reflection;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileDeletionPreflightTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task ChecksEntireSelectionThenConfirmsBeforeAnyMutation(bool fits, bool hasBin, bool consent)
    {
        using var fixture = new Fixture();
        fixture.Bin.Plan = new([new("D:\\", 60, hasBin ? 100 : 0, fits ? 20 : 80)]);
        fixture.Messages.Consent = consent;

        FileOperationBatchResult result = await fixture.Coordinator.DeleteAsync(fixture.Paths);

        Assert.Equal(["plan", "prompt", "answer"], fixture.Events.Take(3));
        Assert.Equal(fixture.Paths, fixture.Bin.PlannedPaths);
        Assert.Equal(!consent, result.Canceled);
        if(!consent)
        {
            Assert.Equal(3, fixture.Events.Count);
            Assert.All(fixture.Paths, path => Assert.True(File.Exists(path)));
            Assert.Equal(0, result.CompletedCount);
            return;
        }
        Assert.Equal(fits ? "recycle-batch" : "permanent-batch", fixture.Events[3]);
        Assert.Equal(3, result.CompletedCount);
        Assert.Equal(fixture.Paths, fixture.Bin.DeletedPaths);
        Assert.Equal(fits ? 0 : 3, result.PermanentlyDeletedCount);
        if(!fits)
            Assert.Contains(LocalizationManager.Format("Explorer.PermanentDeleteWholeSelectionWarning", 3,
                LocalizationManager.Format("Explorer.RecycleSpaceLine", "D:\\", 60 / 1048576.0, 0.0)).Split('\n')[0], fixture.Messages.Message);
    }

    [Fact]
    public async Task UnknownCapacityDoesNotPromptOrDelete()
    {
        using var fixture = new Fixture();
        fixture.Bin.Plan = new([new("D:\\", 60, null, 0)]);
        FileOperationBatchResult result = await fixture.Coordinator.DeleteAsync(fixture.Paths);
        Assert.False(result.Success);
        Assert.Equal(["plan"], fixture.Events);
        Assert.All(fixture.Paths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public async Task CancellationDuringConfirmationDoesNotStartDeletion()
    {
        using var fixture = new Fixture();
        fixture.Bin.Plan = new([new("D:\\", 60, 10, 0)]);
        fixture.Messages.AfterAnswer = fixture.Progress.Cancellation.Cancel;
        FileOperationBatchResult result = await fixture.Coordinator.DeleteAsync(fixture.Paths);
        Assert.True(result.Canceled);
        Assert.Equal(["plan", "prompt", "answer"], fixture.Events);
        Assert.All(fixture.Paths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void SpaceIsComparedPerVolumeIncludingExistingContents()
    {
        var plan = new RecycleBinDeletionPlan([new("C:\\", 20, 100, 90), new("D:\\", 20, 1_000, 0)]);
        Assert.False(plan.Fits);
        Assert.True(plan.IsKnown);
        Assert.True(new RecycleBinVolumeSpace("C:\\", 10, 100, 90).Fits);
        Assert.False(new RecycleBinVolumeSpace("C:\\", 0, 0, 0).Fits);
    }

    [Fact]
    public async Task NativePreflightSumsFilesAndHiddenFolderContentsBeforeDeletion()
    {
        using var directory = new BatchRenameTests.TestDirectory();
        string first = directory.File("first.bin", "1234567890");
        string folder = Directory.CreateDirectory(Path.Combine(directory.Path, "folder")).FullName;
        string nested = Path.Combine(folder, "hidden.bin");
        File.WriteAllBytes(nested, new byte[20]);
        File.SetAttributes(nested, FileAttributes.Hidden);
        RecycleBinDeletionPlan plan = await new WindowsRecycleBinService().PlanDeletionAsync([first, folder]);
        Assert.Equal(30, Assert.Single(plan.Volumes).RequiredBytes);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void CapacityUsesConfiguredMiBAndPolicyAndDoesNotGuessMissingLimit()
    {
        Assert.Equal(10 * 1048576L, WindowsRecycleBinCapacityService.GetCapacityBytes(10, null, false, 1000));
        Assert.Equal(50, WindowsRecycleBinCapacityService.GetCapacityBytes(10, 5, false, 1000));
        Assert.Equal(0, WindowsRecycleBinCapacityService.GetCapacityBytes(10, 5, true, 1000));
        Assert.Null(WindowsRecycleBinCapacityService.GetCapacityBytes(null, null, false, 1000));
    }

    [Fact]
    public void NormalizationHandlesTenThousandSiblingsAndCanonicalDuplicates()
    {
        using var fixture = new Fixture();
        string root = fixture.Directory.Path;
        string[] siblings = Enumerable.Range(0, 10_000).Select(index => Path.Combine(root, $"file-{index}.bin")).ToArray();
        Assert.Equal(siblings, fixture.Coordinator.NormalizeTopLevelSources(siblings));
        string parent = Path.Combine(root, "folder");
        string sibling = Path.Combine(root, "folder-other", "file.bin");
        Assert.Equal([parent, sibling], fixture.Coordinator.NormalizeTopLevelSources([
            Path.Combine(parent, "nested", "file.bin"), parent, Path.Combine(parent, "."), sibling]));
    }

    [Fact]
    public void NormalizationKeepsDifferentAndroidDevicesAndUsesProviderParents()
    {
        var bridge = System.Reflection.DispatchProxy.Create<IWpdStorageBridge, PassiveProxy>();
        var storage = new StorageFacade([new LocalStorageProvider(), new WpdStorageProvider(bridge)]);
        using var fixture = new Fixture(storage);
        string parent = WpdLocatorCodec.Encode("phone-a", "/Download").ToString();
        string child = WpdLocatorCodec.Encode("phone-a", "/Download/file.txt").ToString();
        string otherDevice = WpdLocatorCodec.Encode("phone-b", "/Download/file.txt").ToString();
        string sibling = WpdLocatorCodec.Encode("phone-a", "/Downloads/file.txt").ToString();
        Assert.Equal([parent, otherDevice, sibling], fixture.Coordinator.NormalizeTopLevelSources([child, parent, otherDevice, sibling]));
    }

    [Fact]
    public void NormalizationPreservesCaseSensitiveAndroidPaths()
    {
        var bridge = DispatchProxy.Create<IAndroidStorageBridge, PassiveProxy>();
        var storage = new StorageFacade([new LocalStorageProvider(), new AndroidStorageProvider(bridge)]);
        using var fixture = new Fixture(storage);
        string parent = AndroidLocatorCodec.Encode("phone", "/storage/emulated/0/Folder").ToString();
        string child = AndroidLocatorCodec.Encode("phone", "/storage/emulated/0/Folder/file.txt").ToString();
        string differentCase = AndroidLocatorCodec.Encode("phone", "/storage/emulated/0/folder/file.txt").ToString();
        Assert.Equal([parent, differentCase], fixture.Coordinator.NormalizeTopLevelSources([child, parent, differentCase]));
    }

    private sealed class Fixture: IDisposable
    {
        public BatchRenameTests.TestDirectory Directory { get; } = new();
        public List<string> Events { get; } = [];
        public BinStub Bin { get; }
        public MessageStub Messages { get; }
        public ProgressStub Progress { get; } = new();
        public FileOperationCoordinator Coordinator { get; }
        public string[] Paths { get; }
        public Fixture(IStorageFacade? storage = null)
        {
            Paths = Enumerable.Range(0, 3).Select(index => Directory.File($"file-{index}.txt", new string('x', 20))).ToArray();
            Bin = new(Events);
            Messages = new(Events, Paths);
            Coordinator = new(DispatchProxy.Create<IFileManagerService, PassiveProxy>(), Progress,
                DispatchProxy.Create<IFileConflictResolver, PassiveProxy>(), storage, Bin, Messages);
        }
        public void Dispose() { Progress.Cancellation.Dispose(); Directory.Dispose(); }
    }

    private sealed class MessageStub(List<string> events, string[] paths): IMessageService
    {
        public bool Consent { get; set; } = true;
        public string Message { get; private set; } = "";
        public Action? AfterAnswer { get; set; }
        public Task<Guid> ShowMessage(string title, string message, bool isCanceled = false)
        {
            Assert.All(paths, path => Assert.True(File.Exists(path)));
            Assert.True(isCanceled);
            events.Add("prompt"); Message = message;
            return Task.FromResult(Guid.NewGuid());
        }
        public bool ShowConfirmation(Guid id) { events.Add("answer"); AfterAnswer?.Invoke(); return Consent; }
        public void CloseDialog(Guid id) { }
    }

    private sealed class BinStub(List<string> events): IRecycleBinService
    {
        public RecycleBinDeletionPlan Plan { get; set; } = new([]);
        public string[] PlannedPaths { get; private set; } = [];
        public string[] DeletedPaths { get; private set; } = [];
        public Task<RecycleBinDeletionPlan> PlanDeletionAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
        {
            events.Add("plan"); PlannedPaths = paths.ToArray();
            Assert.All(paths, path => Assert.True(File.Exists(path)));
            return Task.FromResult(Plan);
        }
        public Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Per-file recycling must not be used.");
        public Task<FileOperationBatchResult> RecycleBatchAsync(IReadOnlyList<string> paths, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => Delete(paths, false);
        public Task<FileOperationBatchResult> DeletePermanentlyAsync(IReadOnlyList<string> paths, IProgressReporter? progress = null, CancellationToken cancellationToken = default) => Delete(paths, true);
        private Task<FileOperationBatchResult> Delete(IReadOnlyList<string> paths, bool permanent)
        {
            events.Add(permanent ? "permanent-batch" : "recycle-batch");
            DeletedPaths = paths.ToArray();
            // This stub mutates only the uniquely named disposable test directory.
            foreach(string path in paths) File.Delete(path);
            return Task.FromResult(new FileOperationBatchResult(paths.Select(_ => FileOperationResult.Ok()).ToArray(), paths.Count, 0, false, paths.Count > 0)
                { PermanentlyDeletedCount = permanent ? paths.Count : 0 });
        }
    }

    private sealed class ProgressStub: IProgressDialogService, IProgressReporter
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task<T> RunAsync<T>(string operationName, Func<IProgressReporter, CancellationToken, Task<T>> operation) => operation(this, Cancellation.Token);
        public void Report(double? value, string? currentInfo = null) { }
    }

    public class PassiveProxy: DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Unexpected service call: " + targetMethod?.Name);
    }
}

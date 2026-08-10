using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;
using System.Windows;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ApplicationActivationServiceTests
{
    [WpfFact]
    public async Task RequestsWaitForWindowAndOpenSequentiallyAcrossPipe()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string[] paths =
        [
            Path.Combine(directory, "first.cbook"),
            Path.Combine(directory, "second.cbox"),
            Path.Combine(directory, "third.cbook")
        ];
        foreach(string path in paths)
            await File.WriteAllBytesAsync(path, [1]);
        string missingPath = Path.Combine(directory, "missing.cbook");
        string unsupportedPath = Path.Combine(directory, "unsupported.txt");
        await File.WriteAllTextAsync(unsupportedPath, "unsupported");

        string instanceName = $"CryptoBook.Tests.{Guid.NewGuid():N}";
        string pipeName = $"{instanceName}.Activation";
        var opener = new RecordingFileOpenService(paths.Length);
        var windowManager = new RecordingWindowManager();
        var messages = new RecordingMessageService();

        try
        {
            using var primary = CreateService(
                opener,
                messages,
                windowManager,
                instanceName,
                pipeName);
            Assert.True(await primary.StartAsync(
                [paths[0], missingPath, unsupportedPath]));

            using var secondInstance = CreateService(
                opener,
                messages,
                windowManager,
                instanceName,
                pipeName);
            using var thirdInstance = CreateService(
                opener,
                messages,
                windowManager,
                instanceName,
                pipeName);
            bool[] forwarded = await Task.WhenAll(
                secondInstance.StartAsync([paths[1]]),
                thirdInstance.StartAsync([paths[2]]));

            Assert.All(forwarded, Assert.False);
            Assert.Empty(opener.OpenedPaths);

            Guid mainWindowId = Guid.NewGuid();
            primary.NotifyMainWindowReady(mainWindowId);
            await opener.Completed.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(paths[0], opener.OpenedPaths[0]);
            Assert.Equal(
                paths.Order(StringComparer.OrdinalIgnoreCase),
                opener.OpenedPaths.Order(StringComparer.OrdinalIgnoreCase));
            Assert.Equal(1, opener.MaximumConcurrency);
            Assert.True(windowManager.Activations >= 2);
            Assert.All(
                windowManager.ActivatedWindowIds,
                id => Assert.Equal(mainWindowId, id));
            Assert.Equal(2, messages.Messages.Count);
            Assert.Contains(messages.Messages, message => message.Contains(
                missingPath,
                StringComparison.OrdinalIgnoreCase));
            Assert.Contains(messages.Messages, message => message.Contains(
                unsupportedPath,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TryNormalizePath_TrimsQuotesAndReturnsAbsolutePath()
    {
        string relativePath = Path.Combine("documents", "sample.cbook");

        bool success = ApplicationActivationService.TryNormalizePath(
            $"  \"{relativePath}\"  ",
            out string normalizedPath);

        Assert.True(success);
        Assert.Equal(Path.GetFullPath(relativePath), normalizedPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("bad\0path.cbook")]
    public void TryNormalizePath_RejectsInvalidArguments(string argument)
    {
        Assert.False(ApplicationActivationService.TryNormalizePath(
            argument,
            out _));
    }

    [Theory]
    [InlineData("document.cbook", true)]
    [InlineData("legacy.CBOX", true)]
    [InlineData("document.txt", false)]
    [InlineData("document.cbook.txt", false)]
    public void IsSupportedPath_AllowsOnlyRegisteredCryptoBookFormats(
        string path,
        bool expected)
    {
        Assert.Equal(expected, ApplicationActivationService.IsSupportedPath(path));
    }

    [Fact]
    public void Installer_RegistersOnlyCbookAndCboxForShellOpening()
    {
        string installerPath = FindRepositoryFile(
            "installer",
            "CryptoBook.iss");
        string installer = File.ReadAllText(installerPath);
        string[] associationLines = File.ReadAllLines(installerPath)
            .Where(line => line.Contains(
                "OpenWithProgids",
                StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, associationLines.Length);
        Assert.Contains(associationLines, line => line.Contains(
            "\".cbook\\OpenWithProgids\"",
            StringComparison.Ordinal));
        Assert.Contains(associationLines, line => line.Contains(
            "\".cbox\\OpenWithProgids\"",
            StringComparison.Ordinal));
        Assert.Contains("ChangesAssociations=yes", installer);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while(directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. parts]);
            if(File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(
            Path.DirectorySeparatorChar,
            parts));
    }

    private static ApplicationActivationService CreateService(
        IWorkspaceFileOpenService opener,
        IMessageService messages,
        IWindowManager windowManager,
        string mutexName,
        string pipeName) =>
        new(
            new Lazy<IWorkspaceFileOpenService>(() => opener),
            messages,
            windowManager,
            Dispatcher.CurrentDispatcher,
            mutexName,
            pipeName);

    private sealed class RecordingFileOpenService:
        IWorkspaceFileOpenService
    {
        private readonly int expectedCount;
        private readonly TaskCompletionSource completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> openedPaths = [];
        private int concurrency;
        private int maximumConcurrency;

        public RecordingFileOpenService(int expectedCount)
        {
            this.expectedCount = expectedCount;
        }

        public IReadOnlyList<string> OpenedPaths
        {
            get
            {
                lock(openedPaths)
                    return openedPaths.ToArray();
            }
        }

        public int MaximumConcurrency => Volatile.Read(ref maximumConcurrency);

        public Task Completed => completed.Task;

        public async Task<WorkspaceFileOpenResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            int current = Interlocked.Increment(ref concurrency);
            int observed;
            do
            {
                observed = Volatile.Read(ref maximumConcurrency);
            } while(current > observed && Interlocked.CompareExchange(
                ref maximumConcurrency,
                current,
                observed) != observed);

            try
            {
                await Task.Delay(20, cancellationToken);
                lock(openedPaths)
                {
                    openedPaths.Add(filePath);
                    if(openedPaths.Count == expectedCount)
                        completed.TrySetResult();
                }
                return WorkspaceFileOpenResult.InternalSuccess();
            }
            finally
            {
                Interlocked.Decrement(ref concurrency);
            }
        }
    }

    private sealed class RecordingMessageService: IMessageService
    {
        public List<string> Messages { get; } = [];

        public Task<Guid> ShowMessage(
            string title,
            string message,
            bool isCanceled = false)
        {
            Messages.Add(message);
            return Task.FromResult(Guid.NewGuid());
        }

        public void CloseDialog(Guid id)
        {
        }

        public bool ShowConfirmation(Guid id) => false;
    }

    private sealed class RecordingWindowManager: IWindowManager
    {
        public List<Guid> ActivatedWindowIds { get; } = [];

        public int Activations => ActivatedWindowIds.Count;

        public Guid CreateWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T : Window => Guid.NewGuid();

        public TResult? GetResult<TResult>(Guid guid) => default;

        public void ShowWindow(Guid windowId)
        {
        }

        public void ShowWindowDialog(Guid windowId)
        {
        }

        public void ActivateWindow(Guid windowId) =>
            ActivatedWindowIds.Add(windowId);

        public void CloseWindow(Guid windowId)
        {
        }

        public bool IsWindowOpen(Guid windowId) => true;

        public WindowHost? FindHostWindow(Guid windowId) => null;
    }
}

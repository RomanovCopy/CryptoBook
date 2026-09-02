using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CryptoBook.Performance;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string scenario = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";
        string runRoot = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Performance",
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runRoot);

        try
        {
            if(scenario is "all" or "launch")
                RunLaunchScenario(runRoot);
            if(scenario is "all" or "catalog")
            {
                RunCatalogScenario(runRoot, 1_000);
                RunCatalogScenario(runRoot, 10_000);
            }
            if(scenario is "all" or "search")
                RunSearchScenario(runRoot);
            if(scenario is "all" or "images")
                RunLargeImagesScenario();
            if(scenario is "all" or "encryption")
                RunEncryptionScenario(runRoot);
            return 0;
        }
        finally
        {
            TryDeleteDirectory(runRoot);
        }
    }

    private static void RunLaunchScenario(string runRoot)
    {
        string? executable = FindApplicationExecutable();
        if(executable is null)
        {
            WriteResult("launch", TimeSpan.Zero, 0, new { skipped = "Release executable not found" });
            return;
        }

        string profile = Path.Combine(runRoot, "profile");
        Directory.CreateDirectory(profile);
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        startInfo.Environment["APPDATA"] = Path.Combine(profile, "Roaming");
        startInfo.Environment["LOCALAPPDATA"] = Path.Combine(profile, "Local");

        ForceGc();
        long before = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start CryptoBook.");
        bool idle = process.WaitForInputIdle(30_000);
        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        if(!process.HasExited)
        {
            process.CloseMainWindow();
            if(!process.WaitForExit(5_000))
                process.Kill(entireProcessTree: true);
        }
        WriteResult("launch", stopwatch.Elapsed, allocated, new { idle });
    }

    private static void RunCatalogScenario(string runRoot, int count)
    {
        string directory = Directory.CreateDirectory(
            Path.Combine(runRoot, $"catalog-{count}")).FullName;
        for(int index = 0; index < count; index++)
        {
            using FileStream _ = File.Create(
                Path.Combine(directory, $"file-{index:D6}.txt"));
        }

        DirectoryItem container = CreateContainer();
        (TimeSpan elapsed, long allocated) = Measure(() =>
        {
            ISystemItem[] items = Directory.EnumerateFiles(directory)
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    return (ISystemItem)new FileItem
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        Extension = info.Extension,
                        Size = info.Length,
                        LastWriteTimeUtc = info.LastWriteTimeUtc
                    };
                })
                .ToArray();
            container.AddChildAsync(items, item => item.FullPath)
                .GetAwaiter().GetResult();
            container.SortingAsync(SystemItemSortType.Name)
                .GetAwaiter().GetResult();
        });
        WriteResult(
            $"catalog-{count}",
            elapsed,
            allocated,
            new { items = container.Children.Count });
    }

    private static void RunSearchScenario(string runRoot)
    {
        string workspace = Directory.CreateDirectory(
            Path.Combine(runRoot, "search-workspace")).FullName;
        string indexDirectory = Path.Combine(runRoot, "search-index");
        for(int index = 0; index < 1_000; index++)
        {
            File.WriteAllText(
                Path.Combine(workspace, $"document-{index:D4}.txt"),
                index % 10 == 0
                    ? $"Document {index}. Reproducible search needle."
                    : $"Document {index}. Ordinary searchable content.",
                Encoding.UTF8);
        }

        var searchIndex = new WorkspaceSearchIndex(
            new NeverEncryptedValidator(),
            [new PlainTextDocumentTextExtractor()],
            new WorkspaceContentSearchOptions
            {
                MaxResults = 200,
                IndexDirectory = indexDirectory
            });
        (TimeSpan coldElapsed, long coldAllocated) = Measure(() =>
        {
            searchIndex.UpdateAsync(workspace).GetAwaiter().GetResult();
            _ = searchIndex.SearchAsync(workspace, "needle")
                .GetAwaiter().GetResult();
        });
        (TimeSpan warmElapsed, long warmAllocated) = Measure(() =>
        {
            searchIndex.UpdateAsync(workspace).GetAwaiter().GetResult();
            IReadOnlyList<WorkspaceIndexedDocument> results = searchIndex
                .SearchAsync(workspace, "needle").GetAwaiter().GetResult();
            if(results.Count != 100)
                throw new InvalidOperationException("Unexpected search result count.");
        });

        WriteResult("search-1000-cold", coldElapsed, coldAllocated, new { documents = 1_000 });
        WriteResult("search-1000-warm", warmElapsed, warmAllocated, new { documents = 1_000, matches = 100 });
    }

    private static void RunLargeImagesScenario()
    {
        var document = new FlowDocument();
        var random = new Random(42);
        const int width = 2_048;
        const int height = 2_048;
        const int imageCount = 2;
        for(int index = 0; index < imageCount; index++)
        {
            byte[] pixels = new byte[width * height * 4];
            random.NextBytes(pixels);
            BitmapSource bitmap = BitmapSource.Create(
                width, height, 96, 96, PixelFormats.Bgra32,
                null, pixels, width * 4);
            bitmap.Freeze();
            document.Blocks.Add(new BlockUIContainer(new Image
            {
                Source = bitmap,
                Width = 1_024
            }));
        }

        var previewService = new DocumentPreviewService();
        FlowDocument? preview = null;
        (TimeSpan elapsed, long allocated) = Measure(() =>
            preview = previewService.CreatePreview(document));
        WriteResult(
            "document-large-images",
            elapsed,
            allocated,
            new { images = imageCount, width, height, blocks = preview?.Blocks.Count ?? 0 });
    }

    private static void RunEncryptionScenario(string runRoot)
    {
        const int megabytes = 100;
        string source = Path.Combine(runRoot, "encryption-100mb.bin");
        string encrypted = Path.Combine(runRoot, "encryption-100mb.cbook");
        byte[] block = new byte[1024 * 1024];
        RandomNumberGenerator.Fill(block);
        using(FileStream output = File.Create(source))
        {
            for(int index = 0; index < megabytes; index++)
                output.Write(block);
        }

        using var keyProvider = new PerformanceKeyProvider();
        keyProvider.SetKey("CryptoBook performance scenario");
        var codec = new SecureFileV2Codec(keyProvider, new SecureFileV2Options());
        (TimeSpan elapsed, long allocated) = Measure(() =>
            codec.EncryptFileAsync(source, encrypted).GetAwaiter().GetResult());
        double throughput = megabytes / Math.Max(elapsed.TotalSeconds, 0.001);
        WriteResult(
            "encryption-100mb",
            elapsed,
            allocated,
            new { megabytes, throughputMiBPerSecond = Math.Round(throughput, 2) });
    }

    private static (TimeSpan Elapsed, long Allocated) Measure(Action action)
    {
        ForceGc();
        long before = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return (stopwatch.Elapsed, GC.GetTotalAllocatedBytes(precise: true) - before);
    }

    private static void WriteResult(
        string scenario,
        TimeSpan elapsed,
        long allocatedBytes,
        object details) => Console.WriteLine(JsonSerializer.Serialize(new
        {
            scenario,
            elapsedMilliseconds = Math.Round(elapsed.TotalMilliseconds, 2),
            allocatedMiB = Math.Round(allocatedBytes / 1024d / 1024d, 2),
            details
        }));

    private static void ForceGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string? FindApplicationExecutable()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while(current is not null)
        {
            string candidate = Path.Combine(
                current.FullName, "CryptoBook", "bin", "Release",
                "net10.0-windows10.0.17763.0", "win-x64", "CryptoBook.exe");
            if(File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }
        return null;
    }

    private static DirectoryItem CreateContainer() => new(
        new ImmediateDispatcher(),
        new MonitoringStub(),
        new ItemFactoryStub(),
        new SystemItemSortService());

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if(Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class NeverEncryptedValidator: ISecureFileValidator
    {
        public Task<bool> HasCryptoBookHeaderAsync(
            string filePath,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class PerformanceKeyProvider: IKeyProvider, IDisposable
    {
        private readonly Argon2idKeyDeriver deriver = new();
        private byte[]? password;
        public bool HasKey => password is { Length: > 0 };
        public void SetKey(ReadOnlySpan<char> value)
        {
            Clear();
            password = Encoding.UTF8.GetBytes(value.ToString());
        }
        public byte[] DeriveKey(byte[] salt) => throw new NotSupportedException();
        public Task<byte[]> DeriveKeyAsync(
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default) => deriver.DeriveAsync(
                password ?? throw new InvalidOperationException("Key is not set."),
                salt,
                parameters,
                cancellationToken);
        public void Clear()
        {
            if(password is not null)
                CryptographicOperations.ZeroMemory(password);
            password = null;
        }
        public void Dispose() => Clear();
    }

    private sealed class ImmediateDispatcher: IDispatcherService
    {
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
        public void BeginInvoke(Action action) => action();
        public Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            action();
            return Task.CompletedTask;
        }
        public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Background) =>
            Task.FromResult(func());
    }

    private sealed class MonitoringStub: IDirectoryMonitoringService
    {
        public bool StartMonitoring(
            string directoryPath,
            Action<FileSystemEventArgs>? onCreated = null,
            Action<FileSystemEventArgs>? onDeleted = null,
            Action<RenamedEventArgs>? onRenamed = null,
            Action<FileSystemEventArgs>? onChanged = null,
            Action<Exception?>? onOverflowOrError = null,
            bool includeSubdirectories = false,
            NotifyFilters notifyFilters = NotifyFilters.FileName |
                NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            int internalBufferSize = 64 * 1024) => true;
        public bool StopMonitoring(string directoryPath) => true;
        public void Dispose()
        {
        }
    }

    private sealed class ItemFactoryStub: ISystemItemCreateService
    {
        public IDriveItem CreateRoot(string rootPath) => throw new NotSupportedException();
        public IDirectoryItem CreateDirectory(string path, ISystemItem? parent) =>
            throw new NotSupportedException();
        public IFileItem CreateFile(string path, ISystemItem? parent) =>
            throw new NotSupportedException();
    }
}

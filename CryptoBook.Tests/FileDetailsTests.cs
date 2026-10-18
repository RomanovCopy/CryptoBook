using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileDetailsTests: IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.FileDetails." + Guid.NewGuid());
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");

    public FileDetailsTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
    private static FileDetailsTarget Target(string path) => new(
        Path.GetFileName(path), path, path, new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), false);
    private static WindowContext Context(FileDetailsTarget target) => new(
        new Dictionary<string, object?> { [FileDetailsViewModel.TargetContextKey] = target });

    [Theory]
    [InlineData(false, "Windows PE / EXE")]
    [InlineData(true, "Windows PE / DLL")]
    public async Task ExecutableRenamedToImage_IsDetectedByContent(bool dll, string expected)
    {
        byte[] bytes = new byte[8300];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60, 4), 8192);
        "PE\0\0"u8.CopyTo(bytes.AsSpan(8192));
        if(dll) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8192 + 22, 2), 0x2000);
        string path = Write("photo.jpg", bytes);

        var result = await FileTypeInspector.InspectAsync(path);

        Assert.Equal(expected, result.DetectedType);
        Assert.True(result.HasWarning);
        Assert.Contains(LocalizationManager.GetString("FileDetails.ExecutableAsImage"), result.Messages);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task MalformedMz_IsStillFlaggedWithoutFollowingInvalidOffset(int offset)
    {
        byte[] bytes = new byte[64];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60, 4), offset);
        var result = await FileTypeInspector.InspectAsync(Write("photo.png", bytes));
        Assert.True(result.HasWarning);
        Assert.Contains("MZ", result.DetectedType);
    }

    [Theory]
    [InlineData("photo.png", false)]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.png.exe", true)]
    [InlineData("photo\u202egnp.png", true)]
    public async Task ImageSignature_DoesNotHideSuspiciousName(string name, bool warning)
    {
        var result = await FileTypeInspector.InspectAsync(Write(name, Png));
        Assert.Equal("PNG", result.DetectedType);
        Assert.Equal(warning, result.HasWarning);
        Assert.Contains(LocalizationManager.GetString("FileDetails.ContentLimit"), result.Messages);
        if(name.EndsWith(".exe"))
            Assert.Contains(LocalizationManager.GetString("FileDetails.DoubleExtension"), result.Messages);
    }

    [Fact]
    public async Task ShortcutRenamedToImage_IsDetectedWithoutResolvingTarget()
    {
        byte[] header = [0x4c, 0, 0, 0, 1, 0x14, 2, 0, 0, 0, 0, 0, 0xc0, 0, 0, 0, 0, 0, 0, 0x46];
        var result = await FileTypeInspector.InspectAsync(Write("photo.jpg", header));
        Assert.Contains(".lnk", result.DetectedType);
        Assert.True(result.HasWarning);
    }

    [Fact]
    public async Task UnknownAndEmptyImages_AreNeverConfirmed()
    {
        foreach(byte[] bytes in new[] { Array.Empty<byte>(), "not an image"u8.ToArray() })
        {
            var result = await FileTypeInspector.InspectAsync(Write("empty.jpg", bytes));
            Assert.True(result.HasWarning);
            Assert.Contains(LocalizationManager.GetString("FileDetails.ImageUnconfirmed"), result.Messages);
        }
        Assert.Equal("photo\\u202Egnp.exe", FileTypeInspector.EscapeName("photo\u202egnp.exe"));
    }

    [Fact]
    public async Task Read_CollectsFilesystemAclStreamsAndWindowsPropertyStore()
    {
        string path = Write("sample.png", Png);
        File.WriteAllText(path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://example.test/sample.png\r\n");
        var rows = await new FileDetailsService().ReadAsync(Target(path));
        Assert.Contains(rows, row => row.Key == "Size" && row.Value.StartsWith(Png.Length.ToString()));
        Assert.Contains(rows, row => row.Key == "Owner" && row.Value.Length > 0);
        Assert.Contains(rows, row => row.Key == "Sddl" && row.Value.Contains("D:"));
        Assert.Contains(rows, row => row.Key == "FileId" && row.Value.Length == 16);
        Assert.Contains(rows, row => row.Key == ":Zone.Identifier:$DATA");
        Assert.Contains(rows, row => row.Key == "Zone.Identifier" && row.Value.Contains("HostUrl="));
        Assert.True(rows.Any(row => row.Key == "System.FileName" && row.Value.Contains("sample.png")),
            string.Join(Environment.NewLine, rows.Select(row => $"{row.Category}: {row.Key} = {row.Value}")));
        Assert.Contains(rows, row => row.Key == "System.Image.HorizontalSize" && row.Value.Contains('1'));
        Assert.DoesNotContain(rows, row => row.Key == "Error");
    }

    [Fact]
    public async Task RemoteLocation_ExposesSnapshotWithoutInterpretingOpaqueIdAsPath()
    {
        var target = new FileDetailsTarget("remote.jpg", "wpd://opaque/object", "Phone/DCIM/remote.jpg", 23, DateTime.UtcNow, false);
        Assert.Null(target.LocalPath);
        var rows = await new FileDetailsService().ReadAsync(target);
        Assert.Contains(rows, row => row.Key == "Availability" && row.Value == LocalizationManager.GetString("FileDetails.RemoteLimit"));
        Assert.Contains(rows, row => row.Key == "Size" && row.Value.StartsWith("23"));
    }

    [Fact]
    public async Task DirectoryProperties_IncludeIdentityWithoutTreatingItAsFileData()
    {
        var target = new FileDetailsTarget("directory", directory, directory, 0, DateTime.UtcNow, true);
        var rows = await new FileDetailsService().ReadAsync(target);
        Assert.Contains(rows, row => row.Key == "FileId");
        Assert.Contains(rows, row => row.Key == "AllocationSize");
        Assert.DoesNotContain(rows, row => row.Key == "StoredSize");
        Assert.DoesNotContain(rows, row => row.Key == "Error");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public async Task Hashes_MatchKnownAlgorithms(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var rows = await new FileDetailsService().ComputeHashesAsync(Write("hash.txt", bytes));
        Assert.Contains(rows, row => row.Key == "SHA256" && row.Value == Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.Contains(rows, row => row.Key == "SHA512" && row.Value == Convert.ToHexString(SHA512.HashData(bytes)));
    }

    [Fact]
    public async Task HashingAndReading_RespectCancellation()
    {
        string path = Write("hash.txt", "abc"u8.ToArray());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new FileDetailsService();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ComputeHashesAsync(path, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReadAsync(Target(path), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileTypeInspector.InspectAsync(path, cancellation.Token));
    }

    [Fact]
    public async Task Signature_ReadsEmbeddedSignerAndReportsUnsignedFiles()
    {
        var service = new FileDetailsService();
        var unsigned = await service.ReadSignatureAsync(Write("unsigned.txt", "abc"u8.ToArray()));
        Assert.Contains(unsigned, row => row.Key == "Status" && row.Value == LocalizationManager.GetString("FileDetails.SignatureNotSigned"));
        string signedPath = Path.GetFullPath(Path.Combine(
            System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        var signed = await service.ReadSignatureAsync(signedPath);
        Assert.Contains(signed, row => row.Key == "Subject" && row.Value.Contains("Microsoft", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(signed, row => row.Key == "Thumbprint" && row.Value.Length > 0);
        Assert.DoesNotContain(signed, row => row.Key == "Error");
        string catalogSignedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        var catalogSigned = await service.ReadSignatureAsync(catalogSignedPath);
        Assert.Contains(catalogSigned, row => row.Key == "Catalog[0].Subject" && row.Value.Contains("Microsoft"));
        Assert.Contains(catalogSigned, row => row.Key == "Catalog[0].WinVerifyTrust.Result" && row.Value == "0x00000000");
        Assert.DoesNotContain(catalogSigned, row => row.Key == "Error");
    }

    [Fact]
    public void PeHeaders_AreReadAsDataIncludingManagedAssemblyMetadata()
    {
        var rows = new List<FilePropertyEntry>();
        FileDetailsService.ReadPeHeaders(typeof(FileDetailsService).Assembly.Location, rows);
        Assert.Contains(rows, row => row.Key == "COFF.Machine");
        Assert.Contains(rows, row => row.Key == "PE.Subsystem");
        Assert.Contains(rows, row => row.Key == "Assembly.Name" && row.Value == "CryptoBook");
        Assert.Contains(rows, row => row.Key.EndsWith(".RVA") && row.Value.StartsWith("0x"));
    }

    [WpfFact]
    public async Task ViewModel_FiltersAndCopiesFullValuesAndRetainsCollectedData()
    {
        string path = Write("photo.png", Png);
        using var model = new FileDetailsViewModel(new StubService(), Context(Target(path)));
        await model.LoadAsync();
        Assert.Equal("PNG", model.DetectedType);
        Assert.False(model.HasWarning);
        model.SearchText = "very long value";
        Assert.Single(model.VisibleRows.Cast<FilePropertyEntry>());
        Assert.Contains("very long value\nsecond line", model.CreateReport());
        await model.ComputeHashesAsync();
        Assert.Contains(model.Rows, row => row.Key == "SHA256");
        Assert.False(model.IsBusy);
    }

    [WpfFact]
    public async Task CancelledHashResult_IsDiscardedAndExistingPropertiesAreRetained()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<FilePropertyEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StubService { HashGate = gate };
        using var model = new FileDetailsViewModel(service, Context(Target(Write("photo.png", Png))));
        await model.LoadAsync();
        int count = model.Rows.Count;
        Task hash = model.ComputeHashesAsync();
        Assert.True(model.IsBusy);
        model.CancelCommand.Execute(null);
        gate.SetResult([new("Hashes", "SHA-256", "stale", "SHA256")]);
        await hash;
        Assert.False(model.IsBusy);
        Assert.Equal(count, model.Rows.Count);
        Assert.DoesNotContain(model.Rows, row => row.Key == "SHA256");
        Assert.Equal(LocalizationManager.GetString("FileDetails.Cancelled"), model.Status);
    }

    [WpfFact]
    public async Task Window_ConstructsAndRendersWithWarningAndLongProperties()
    {
        string path = Write("photo.jpg.exe", Png);
        using var model = new FileDetailsViewModel(new StubService(), Context(Target(path)));
        await model.LoadAsync();
        var window = new FileDetailsWindow(model);
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/DarkTheme.xaml", UriKind.Relative) });
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Styles/ThemedControls.xaml", UriKind.Relative) });
        try
        {
            var content = (FrameworkElement)window.Content;
            content.Measure(new System.Windows.Size(1080, 700));
            content.Arrange(new Rect(0, 0, 1080, 700));
            content.UpdateLayout();
            Assert.True(model.HasWarning);
            var grid = Assert.IsType<DataGrid>(window.FindName("PropertiesGrid"));
            Assert.Equal(4, grid.Columns.Count);
            Assert.True(grid.Items.Count > 0);
            string? renderPath = Environment.GetEnvironmentVariable("CRYPTOBOOK_DETAILS_RENDER");
            if(!string.IsNullOrEmpty(renderPath))
            {
                var bitmap = new RenderTargetBitmap(1080, 700, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(renderPath);
                encoder.Save(output);
            }
        }
        finally { window.Close(); }
    }

    private sealed class StubService: IFileDetailsService
    {
        public TaskCompletionSource<IReadOnlyList<FilePropertyEntry>>? HashGate { get; init; }
        public Task<IReadOnlyList<FilePropertyEntry>> ReadAsync(FileDetailsTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FilePropertyEntry>>([new("Metadata", "Example", "very long value\nsecond line", "System.Example")]);
        public Task<IReadOnlyList<FilePropertyEntry>> ReadSignatureAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FilePropertyEntry>>([]);
        public Task<IReadOnlyList<FilePropertyEntry>> ComputeHashesAsync(string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
            HashGate?.Task ?? Task.FromResult<IReadOnlyList<FilePropertyEntry>>([new("Hashes", "SHA-256", "ABCD", "SHA256")]);
    }
}

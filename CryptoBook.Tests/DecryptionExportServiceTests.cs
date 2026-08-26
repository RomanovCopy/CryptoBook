using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests;

public sealed class DecryptionExportServiceTests: IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"CryptoBook-export-tests-{Guid.NewGuid():N}");

    public DecryptionExportServiceTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public async Task Original_PublishesExactDecryptedBytes()
    {
        byte[] payload = [0, 1, 2, 3, 254, 255];
        DecryptionExportService service = CreateService(payload, ".XamlPackage");
        string source = CreateSource("book.cbook");
        await using PreparedDecryption prepared = await service.PrepareAsync(source);

        string result = await service.PublishAsync(
            prepared,
            new DecryptionOptions(
                EncryptionTargetMode.SaveAs,
                DecryptionOutputFormat.Original),
            Path.Combine(directory, "book_decrypted.any"));

        Assert.Equal(".XamlPackage", Path.GetExtension(result));
        Assert.Equal(payload, await File.ReadAllBytesAsync(result));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void XamlPackage_DefaultsToRtf_AndUnknownOffersOnlyOriginal()
    {
        DecryptionExportService convertible = CreateService(
            [1],
            ".XamlPackage");
        DecryptionExportService unsupported = CreateService(
            [1],
            ".png",
            canConvert: false);

        Assert.Equal(
            DecryptionOutputFormat.Rtf,
            convertible.GetDefaultFormat(".XamlPackage"));
        Assert.Equal(
            [
                DecryptionOutputFormat.Rtf,
                DecryptionOutputFormat.PlainText,
                DecryptionOutputFormat.Original
            ],
            convertible.GetAvailableFormats(".XamlPackage"));
        Assert.Equal(
            [DecryptionOutputFormat.Original],
            unsupported.GetAvailableFormats(".png"));
    }

    [Fact]
    public async Task SaveCopy_DoesNotDeleteProtectedSource()
    {
        DecryptionExportService service = CreateService([7, 8], ".txt");
        string source = CreateSource("copy.cbook");
        await using PreparedDecryption prepared = await service.PrepareAsync(source);

        string result = await service.PublishAsync(
            prepared,
            new DecryptionOptions(
                EncryptionTargetMode.SaveAs,
                DecryptionOutputFormat.Original),
            Path.Combine(directory, "copy_decrypted.txt"));

        Assert.True(File.Exists(source));
        Assert.True(File.Exists(result));
    }

    [Theory]
    [InlineData("replace.cbook")]
    [InlineData("replace.cbox")]
    public async Task ReplaceSource_DeletesEitherContainerOnlyAfterPublish(
        string containerName)
    {
        DecryptionExportService service = CreateService([4, 5, 6], ".txt");
        string source = CreateSource(containerName);
        await using PreparedDecryption prepared = await service.PrepareAsync(source);

        string result = await service.PublishAsync(
            prepared,
            new DecryptionOptions(
                EncryptionTargetMode.ReplaceSource,
                DecryptionOutputFormat.Original),
            Path.Combine(directory, "replace.txt"));

        Assert.True(File.Exists(result));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task ConversionFailure_KeepsProtectedSourceAndPublishesNothing()
    {
        DecryptionExportService service = CreateService(
            [1, 2, 3],
            ".XamlPackage",
            conversionFailure: new InvalidDataException("broken document"));
        string source = CreateSource("failure.cbook");
        await using PreparedDecryption prepared = await service.PrepareAsync(source);
        string target = Path.Combine(directory, "failure.rtf");

        await Assert.ThrowsAsync<IOException>(() => service.PublishAsync(
            prepared,
            new DecryptionOptions(
                EncryptionTargetMode.ReplaceSource,
                DecryptionOutputFormat.Rtf),
            target));

        Assert.True(File.Exists(source));
        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(directory, ".*.tmp"));
    }

    [Fact]
    public async Task PrepareCancellation_RemovesTemporaryPlaintext()
    {
        using var cancellation = new CancellationTokenSource();
        DecryptionExportService service = CreateService(
            [1, 2, 3],
            ".txt",
            cancelDuringDecrypt: cancellation);
        string source = CreateSource("cancel.cbook");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.PrepareAsync(
                source,
                cancellationToken: cancellation.Token));

        string temporaryRoot = Path.Combine(directory, "temporary");
        Assert.Empty(Directory.GetDirectories(temporaryRoot));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task ExistingDestination_IsPreservedAndUniqueNameIsUsed()
    {
        DecryptionExportService service = CreateService([3, 2, 1], ".rtf");
        string source = CreateSource("conflict.cbook");
        string existing = Path.Combine(directory, "conflict.rtf");
        await File.WriteAllBytesAsync(existing, [9, 9, 9]);
        await using PreparedDecryption prepared = await service.PrepareAsync(source);

        string result = await service.PublishAsync(
            prepared,
            new DecryptionOptions(
                EncryptionTargetMode.ReplaceSource,
                DecryptionOutputFormat.Original),
            existing);

        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(existing));
        Assert.Equal("conflict (2).rtf", Path.GetFileName(result));
        Assert.Equal([3, 2, 1], await File.ReadAllBytesAsync(result));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task DisposingPreparedResult_RemovesTemporaryData()
    {
        DecryptionExportService service = CreateService([1], ".txt");
        string source = CreateSource("dispose.cbook");
        PreparedDecryption prepared = await service.PrepareAsync(source);
        string temporaryDirectory = prepared.TemporaryDirectory;

        await prepared.DisposeAsync();

        Assert.False(Directory.Exists(temporaryDirectory));
        Assert.True(File.Exists(source));
    }

    public void Dispose()
    {
        if(Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private DecryptionExportService CreateService(
        byte[] payload,
        string originalExtension,
        bool canConvert = true,
        Exception? conversionFailure = null,
        CancellationTokenSource? cancelDuringDecrypt = null) =>
        new(
            new StubSecureFileProcessor(
                payload,
                originalExtension,
                cancelDuringDecrypt),
            new AlwaysEncryptedValidator(),
            new StubConversionService(canConvert, conversionFailure),
            Path.Combine(directory, "temporary"));

    private string CreateSource(string name)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, [99]);
        return path;
    }

    private sealed class StubConversionService:
        IDecryptedDocumentConversionService
    {
        private readonly bool canConvert;
        private readonly Exception? failure;

        public StubConversionService(bool canConvert, Exception? failure)
        {
            this.canConvert = canConvert;
            this.failure = failure;
        }

        public bool CanConvert(string originalExtension) => canConvert;

        public async Task ConvertAsync(
            Stream source,
            string originalExtension,
            DecryptionOutputFormat targetFormat,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            if(failure is not null)
                throw failure;
            await source.CopyToAsync(destination, cancellationToken);
        }
    }

    private sealed class StubSecureFileProcessor: ISecureFileProcessor
    {
        private readonly byte[] payload;
        private readonly string originalExtension;
        private readonly CancellationTokenSource? cancelDuringDecrypt;

        public StubSecureFileProcessor(
            byte[] payload,
            string originalExtension,
            CancellationTokenSource? cancelDuringDecrypt)
        {
            this.payload = payload;
            this.originalExtension = originalExtension;
            this.cancelDuringDecrypt = cancelDuringDecrypt;
        }

        public Task<DecryptedFileContent> DecryptFileContentAsync(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancelDuringDecrypt?.Cancel();
            return Task.FromResult(new DecryptedFileContent(
                new MemoryStream(payload, writable: false),
                originalExtension));
        }

        public Task EncryptFileAsync(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EncryptStreamAsync(
            Stream input,
            string originalExtension,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DecryptFileAsyncToFile(
            string inputFile,
            string outputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> DecryptFileAsyncToStream(
            string inputFile,
            IProgressReporter? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class AlwaysEncryptedValidator: ISecureFileValidator
    {
        public Task<bool> HasCryptoBookHeaderAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}

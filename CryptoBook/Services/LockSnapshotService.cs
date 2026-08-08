using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Documents;

namespace CryptoBook.Services;

/// <summary>
/// Создаёт зашифрованный снимок последнего документа. Версия 2 хранит байты
/// XamlPackage напрямую и продолжает читать прежний JSON/Base64-контейнер.
/// </summary>
public sealed class LockSnapshotService: ILockSnapshotService
{
    private static readonly byte[] SnapshotMagic =
        Encoding.ASCII.GetBytes("CBLOCK02");

    private readonly ISecureFileProcessor secureFileProcessor;
    private readonly IFlowDocumentSaveService saveService;
    private readonly IFlowDocumentLoadService loadService;
    private readonly IFileTemplate snapshotTemplate = new SecureFileTemplate();

    public LockSnapshotService(
        ISecureFileProcessor secureFileProcessor,
        IFlowDocumentSaveService saveService,
        IFlowDocumentLoadService loadService)
    {
        this.secureFileProcessor = secureFileProcessor ??
            throw new ArgumentNullException(nameof(secureFileProcessor));
        this.saveService = saveService ??
            throw new ArgumentNullException(nameof(saveService));
        this.loadService = loadService ??
            throw new ArgumentNullException(nameof(loadService));
    }

    public string SnapshotPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CryptoBook",
        "Lock",
        "last.lock.cbook");

    public bool Exists => File.Exists(SnapshotPath);

    public async Task CreateAndVerifyAsync(
        IRichTextBoxService richTextBox,
        LockSnapshotMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(richTextBox);
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();

        string? temporaryPath = null;
        try
        {
            await using var document = new MemoryStream();
            await saveService.SaveToStreamAsync(
                richTextBox,
                document,
                snapshotTemplate,
                cancellationToken);
            await using var envelope = new MemoryStream();
            await BinarySnapshotEnvelope.WriteHeaderAsync(
                envelope,
                SnapshotMagic,
                metadata,
                cancellationToken);
            document.Position = 0;
            await document.CopyToAsync(envelope, cancellationToken);
            envelope.Position = 0;

            string directory = Path.GetDirectoryName(SnapshotPath)!;
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(SnapshotPath)}.{Guid.NewGuid():N}.tmp");
            await secureFileProcessor.EncryptStreamAsync(
                envelope,
                ".cbook",
                temporaryPath,
                cancellationToken: cancellationToken);

            await VerifyFileAsync(temporaryPath, cancellationToken);
            File.Move(temporaryPath, SnapshotPath, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            if(temporaryPath is not null)
                TryDelete(temporaryPath);
        }
    }

    public async Task<(FlowDocument Document, LockSnapshotMetadata Metadata)>
        ReadAndVerifyAsync(CancellationToken cancellationToken = default)
    {
        if(!Exists)
            throw new FileNotFoundException("Защищённый снимок не найден.");

        await using DecryptedFileContent decrypted = await secureFileProcessor
            .DecryptFileContentAsync(
                SnapshotPath,
                cancellationToken: cancellationToken);
        return await ReadEnvelopeAsync(decrypted.Content, cancellationToken);
    }

    public void Delete() => TryDelete(SnapshotPath);

    private async Task VerifyFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using DecryptedFileContent decrypted = await secureFileProcessor
            .DecryptFileContentAsync(path, cancellationToken: cancellationToken);
        _ = await ReadEnvelopeAsync(decrypted.Content, cancellationToken);
    }

    private async Task<(FlowDocument Document, LockSnapshotMetadata Metadata)>
        ReadEnvelopeAsync(
            Stream stream,
            CancellationToken cancellationToken)
    {
        LockSnapshotMetadata? metadata = await BinarySnapshotEnvelope
            .TryReadHeaderAsync<LockSnapshotMetadata>(
                stream,
                SnapshotMagic,
                cancellationToken);
        if(metadata is not null)
        {
            await using Stream documentSource =
                BinarySnapshotEnvelope.OpenPayloadStream(stream);
            FlowDocument document = await loadService.PrepareAsync(
                documentSource,
                snapshotTemplate,
                cancellationToken);
            return (document, metadata);
        }

        // Совместимость со снимками версии 1: JSON с Base64-документом.
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        string json = await reader.ReadToEndAsync(cancellationToken);
        LegacySnapshotEnvelope envelope =
            JsonSerializer.Deserialize<LegacySnapshotEnvelope>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Снимок имеет неверный формат.");
        byte[] documentBytes = Convert.FromBase64String(envelope.Document);
        try
        {
            FlowDocument document = await loadService.PrepareAsync(
                new MemoryStream(documentBytes, writable: false),
                snapshotTemplate,
                cancellationToken);
            return (document, envelope.Metadata);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(documentBytes);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if(File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private sealed record LegacySnapshotEnvelope(
        LockSnapshotMetadata Metadata,
        string Document);
}

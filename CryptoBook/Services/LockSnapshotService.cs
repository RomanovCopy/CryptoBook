using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Documents;

namespace CryptoBook.Services;

/// <summary>
/// Создаёт единственный зашифрованный снимок последнего документа. В файл
/// попадает только зашифрованный конверт: исходный обычный файл не изменяется.
/// </summary>
public sealed class LockSnapshotService : ILockSnapshotService
{
    private readonly ISecureFileProcessor secureFileProcessor;
    private readonly IFlowDocumentSaveService saveService;
    private readonly IFlowDocumentLoadService loadService;
    private readonly IFileTemplate snapshotTemplate = new SecureFileTemplate();
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public LockSnapshotService(
        ISecureFileProcessor secureFileProcessor,
        IFlowDocumentSaveService saveService,
        IFlowDocumentLoadService loadService)
    {
        this.secureFileProcessor = secureFileProcessor ?? throw new ArgumentNullException(nameof(secureFileProcessor));
        this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        this.loadService = loadService ?? throw new ArgumentNullException(nameof(loadService));
    }

    public string SnapshotPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CryptoBook", "Lock", "last.lock.cbook");

    public bool Exists => File.Exists(SnapshotPath);

    public async Task CreateAndVerifyAsync(
        IRichTextBoxService richTextBox,
        LockSnapshotMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(richTextBox);
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? documentBytes = null;
        byte[]? envelopeBytes = null;
        string? temporaryPath = null;
        try
        {
            await using var documentStream = new MemoryStream();
            await saveService.SaveToStreamAsync(richTextBox, documentStream, snapshotTemplate, cancellationToken);
            documentBytes = documentStream.ToArray();
            var envelope = new SnapshotEnvelope(metadata, Convert.ToBase64String(documentBytes));
            envelopeBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, jsonOptions));

            string directory = Path.GetDirectoryName(SnapshotPath)!;
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(SnapshotPath)}.{Guid.NewGuid():N}.tmp");
            await using (var input = new MemoryStream(envelopeBytes, writable: false))
            {
                await secureFileProcessor.EncryptStreamAsync(input, ".cbook", temporaryPath, cancellationToken: cancellationToken);
            }

            // Проверяем именно текущим ключом до публикации снимка.
            await VerifyFileAsync(temporaryPath, cancellationToken);
            File.Move(temporaryPath, SnapshotPath, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            if(temporaryPath is not null)
                TryDelete(temporaryPath);
            if(documentBytes is not null)
                CryptographicOperations.ZeroMemory(documentBytes);
            if(envelopeBytes is not null)
                CryptographicOperations.ZeroMemory(envelopeBytes);
        }
    }

    public async Task<(FlowDocument Document, LockSnapshotMetadata Metadata)> ReadAndVerifyAsync(
        CancellationToken cancellationToken = default)
    {
        if(!Exists)
            throw new FileNotFoundException("Защищённый снимок не найден.");

        DecryptedFileContent decrypted = await secureFileProcessor.DecryptFileContentAsync(SnapshotPath, cancellationToken: cancellationToken);
        await using Stream content = decrypted.Content;
        SnapshotEnvelope envelope = await ReadEnvelopeAsync(content, cancellationToken);
        byte[] documentBytes = Convert.FromBase64String(envelope.Document);
        try
        {
            FlowDocument document = await loadService.PrepareAsync(
                new MemoryStream(documentBytes, writable: false), snapshotTemplate, cancellationToken);
            return (document, envelope.Metadata);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(documentBytes);
        }
    }

    public void Delete() => TryDelete(SnapshotPath);

    private async Task VerifyFileAsync(string path, CancellationToken cancellationToken)
    {
        DecryptedFileContent decrypted = await secureFileProcessor.DecryptFileContentAsync(path, cancellationToken: cancellationToken);
        await using Stream content = decrypted.Content;
        SnapshotEnvelope envelope = await ReadEnvelopeAsync(content, cancellationToken);
        if(string.IsNullOrWhiteSpace(envelope.Document) || string.IsNullOrWhiteSpace(envelope.Metadata.TemplateId))
            throw new InvalidDataException("Снимок имеет неверный формат.");
        byte[] bytes = Convert.FromBase64String(envelope.Document);
        try
        {
            _ = await loadService.PrepareAsync(new MemoryStream(bytes, writable: false), snapshotTemplate, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static async Task<SnapshotEnvelope> ReadEnvelopeAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        string json = await reader.ReadToEndAsync(cancellationToken);
        SnapshotEnvelope? envelope = JsonSerializer.Deserialize<SnapshotEnvelope>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return envelope ?? throw new InvalidDataException("Снимок имеет неверный формат.");
    }

    private static void TryDelete(string path)
    {
        try { if(File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed record SnapshotEnvelope(LockSnapshotMetadata Metadata, string Document);
}

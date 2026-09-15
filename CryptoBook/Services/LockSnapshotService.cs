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
    private readonly IFileTemplateRegistry? templateRegistry;
    private readonly IMarkdownDocumentState? markdownDocument;
    private readonly PasswordProtectedBuffer? snapshotProtection;
    private readonly LockSnapshotNoticeStore noticeStore = new();

    public LockSnapshotService(
        ISecureFileProcessor secureFileProcessor,
        IFlowDocumentSaveService saveService,
        IFlowDocumentLoadService loadService,
        IFileTemplateRegistry? templateRegistry = null,
        IMarkdownDocumentState? markdownDocument = null,
        IKeyProvider? keyProvider = null)
        : this(
            secureFileProcessor,
            saveService,
            loadService,
            GetDefaultSnapshotPath(),
            templateRegistry,
            markdownDocument,
            keyProvider)
    {
    }

    internal LockSnapshotService(
        ISecureFileProcessor secureFileProcessor,
        IFlowDocumentSaveService saveService,
        IFlowDocumentLoadService loadService,
        string snapshotPath,
        IFileTemplateRegistry? templateRegistry = null,
        IMarkdownDocumentState? markdownDocument = null,
        IKeyProvider? keyProvider = null)
    {
        this.secureFileProcessor = secureFileProcessor ??
            throw new ArgumentNullException(nameof(secureFileProcessor));
        this.saveService = saveService ??
            throw new ArgumentNullException(nameof(saveService));
        this.loadService = loadService ??
            throw new ArgumentNullException(nameof(loadService));
        this.templateRegistry = templateRegistry;
        this.markdownDocument = markdownDocument;
        snapshotProtection = keyProvider is null ? null : new PasswordProtectedBuffer(keyProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);
        SnapshotPath = Path.GetFullPath(snapshotPath);
        PromotePendingSnapshot();
    }

    public string SnapshotPath { get; }

    private static string GetDefaultSnapshotPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CryptoBook",
        "Lock",
        "last.lock.cbook");

    public bool Exists => File.Exists(SnapshotPath);
    public LockSnapshotNotice? GetNotice() => noticeStore.Read(SnapshotPath);
    public void DismissNotice() => noticeStore.Dismiss(SnapshotPath);

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
            bool isMarkdown = markdownDocument?.IsActive == true;
            IFileTemplate contentTemplate = isMarkdown
                ? templateRegistry?.GetById("Markdown")
                    ?? new MarkdownFileTemplate()
                : snapshotTemplate;
            LockSnapshotMetadata snapshotMetadata = isMarkdown
                ? metadata with { ContentTemplateId = "Markdown" }
                : metadata;
            await using var document = new SensitiveMemoryStream();
            await saveService.SaveToStreamAsync(
                richTextBox,
                document,
                contentTemplate,
                cancellationToken);
            await using var envelope = new SensitiveMemoryStream();
            await BinarySnapshotEnvelope.WriteHeaderAsync(
                envelope,
                SnapshotMagic,
                snapshotMetadata,
                cancellationToken);
            await WorkspaceSnapshotPayload.WriteAsync(envelope, document,
                metadata.InactiveDocument, cancellationToken);
            envelope.Position = 0;

            string directory = Path.GetDirectoryName(SnapshotPath)!;
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(SnapshotPath)}.{Guid.NewGuid():N}.tmp");
            if(snapshotProtection is not null)
            {
                byte[] encrypted = await snapshotProtection.ProtectAsync(
                    envelope.GetBuffer().AsMemory(0, checked((int)envelope.Length)), cancellationToken);
                await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken);
            }
            else
            {
                await secureFileProcessor.EncryptStreamAsync(envelope, ".cbook", temporaryPath,
                    cancellationToken: cancellationToken);
            }

            await VerifyFileAsync(temporaryPath, cancellationToken);
            // Continuing without restoring must not overwrite an earlier unsaved document.
            if(Exists)
            {
                string pending = SnapshotPath + "." + Guid.NewGuid().ToString("N") + ".pending";
                File.Copy(SnapshotPath, pending);
                LockSnapshotNoticeStore.Copy(SnapshotPath, pending);
            }
            File.Move(temporaryPath, SnapshotPath, overwrite: true);
            temporaryPath = null;
            LockSnapshotNoticeStore.Save(SnapshotPath, snapshotMetadata);
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

        var result = await ReadFileAsync(SnapshotPath, cancellationToken);
        LockSnapshotNoticeStore.Save(SnapshotPath, result.Metadata);
        return result;
    }

    private async Task<(FlowDocument Document, LockSnapshotMetadata Metadata)> ReadFileAsync(
        string path, CancellationToken cancellationToken)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if(PasswordProtectedBuffer.HasHeader(bytes))
        {
            if(snapshotProtection is null)
                throw new CryptographicException("Для восстановления требуется ключ CryptoBook.");
            byte[] plaintext = await snapshotProtection.UnprotectAsync(bytes, cancellationToken);
            try
            {
                using var stream = new MemoryStream(plaintext, writable: false);
                return await ReadEnvelopeAsync(stream, cancellationToken);
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        await using DecryptedFileContent decrypted = await secureFileProcessor
            .DecryptFileContentAsync(
                path,
                cancellationToken: cancellationToken);
        return await ReadEnvelopeAsync(decrypted.Content, cancellationToken);
    }

    public void Delete()
    {
        TryDelete(SnapshotPath);
        if(!Exists) LockSnapshotNoticeStore.Delete(SnapshotPath);
        PromotePendingSnapshot();
    }

    private void PromotePendingSnapshot()
    {
        string? directory = Path.GetDirectoryName(SnapshotPath);
        if(Exists || directory is null || !Directory.Exists(directory))
            return;
        string? pending = Directory.EnumerateFiles(directory, Path.GetFileName(SnapshotPath) + ".*.pending")
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if(pending is not null)
        {
            File.Move(pending, SnapshotPath);
            LockSnapshotNoticeStore.Copy(pending, SnapshotPath);
            LockSnapshotNoticeStore.Delete(pending);
        }
    }

    private async Task VerifyFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        _ = await ReadFileAsync(path, cancellationToken);
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
            var workspace = await WorkspaceSnapshotPayload.ReadAsync(documentSource, cancellationToken);
            await using var activeSource = workspace.Active;
            IFileTemplate contentTemplate = ResolveContentTemplate(
                metadata.ContentTemplateId);
            FlowDocument document = await loadService.PrepareAsync(
                activeSource,
                contentTemplate,
                cancellationToken);
            return (document, metadata with { InactiveDocument = workspace.Inactive });
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

    private IFileTemplate ResolveContentTemplate(string? templateId)
    {
        if(string.IsNullOrWhiteSpace(templateId))
            return snapshotTemplate;
        if(string.Equals(
            templateId,
            "Markdown",
            StringComparison.OrdinalIgnoreCase))
        {
            return templateRegistry?.GetById("Markdown")
                ?? new MarkdownFileTemplate();
        }

        throw new InvalidDataException(
            $"Lock snapshot content format '{templateId}' is unavailable.");
    }

    private sealed record LegacySnapshotEnvelope(
        LockSnapshotMetadata Metadata,
        string Document);
}

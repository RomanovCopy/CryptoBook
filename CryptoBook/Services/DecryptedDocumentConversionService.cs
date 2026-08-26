using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public sealed class DecryptedDocumentConversionService:
        IDecryptedDocumentConversionService
    {
        private readonly IFileTemplateRegistry templates;
        private readonly IDocumentFormatHandlerRegistry handlers;
        private readonly IDispatcherService dispatcher;

        public DecryptedDocumentConversionService(
            IFileTemplateRegistry templates,
            IDocumentFormatHandlerRegistry handlers,
            IDispatcherService dispatcher)
        {
            this.templates = templates ??
                throw new ArgumentNullException(nameof(templates));
            this.handlers = handlers ??
                throw new ArgumentNullException(nameof(handlers));
            this.dispatcher = dispatcher ??
                throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool CanConvert(string originalExtension) =>
            ResolveSourceTemplate(originalExtension) is not null;

        public async Task ConvertAsync(
            Stream source,
            string originalExtension,
            DecryptionOutputFormat targetFormat,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            if(!source.CanRead)
                throw new ArgumentException("Source stream must be readable.", nameof(source));
            if(!destination.CanWrite)
                throw new ArgumentException("Destination stream must be writable.", nameof(destination));
            if(targetFormat == DecryptionOutputFormat.Original)
            {
                await source.CopyToAsync(destination, cancellationToken);
                return;
            }

            IFileTemplate sourceTemplate = ResolveSourceTemplate(originalExtension)
                ?? throw new NotSupportedException(
                    LocalizationManager.GetString(
                        "DecryptionExport.UnsupportedConversion"));
            IFileTemplate targetTemplate = ResolveTargetTemplate(targetFormat)
                ?? throw new NotSupportedException(
                    LocalizationManager.GetString(
                        "DecryptionExport.UnsupportedConversion"));
            IDocumentFormatHandler sourceHandler = handlers.Find(sourceTemplate)
                ?? throw new NotSupportedException(
                    LocalizationManager.GetString(
                        "DecryptionExport.UnsupportedConversion"));
            IDocumentFormatHandler targetHandler = handlers.Find(targetTemplate)
                ?? throw new NotSupportedException(
                    LocalizationManager.GetString(
                        "DecryptionExport.UnsupportedConversion"));

            byte[] content = await ReadAllBytesAsync(source, cancellationToken);
            FlowDocument document = await dispatcher.InvokeAsync(
                () => new FlowDocument());
            await sourceHandler.LoadAsync(document, content, cancellationToken);
            byte[] converted = await targetHandler.SerializeAsync(
                document,
                cancellationToken);
            await destination.WriteAsync(converted, cancellationToken);
        }

        private IFileTemplate? ResolveSourceTemplate(string extension)
        {
            string normalized = NormalizeExtension(extension);
            return templates.GetAll().FirstOrDefault(template =>
                (template is XamlPackageFileTemplate or
                    RichTextFileTemplate or PlainTextTemplate) &&
                IsExplicitlySupportedExtension(template, normalized) &&
                handlers.Find(template) is not null);
        }

        private IFileTemplate? ResolveTargetTemplate(
            DecryptionOutputFormat targetFormat) =>
            templates.GetAll().FirstOrDefault(template => targetFormat switch
            {
                DecryptionOutputFormat.Rtf => template is RichTextFileTemplate,
                DecryptionOutputFormat.PlainText => template is PlainTextTemplate,
                _ => false
            });

        private static bool IsExplicitlySupportedExtension(
            IFileTemplate template,
            string extension) =>
            template switch
            {
                XamlPackageFileTemplate => extension.Equals(
                    ".XamlPackage",
                    StringComparison.OrdinalIgnoreCase),
                RichTextFileTemplate => extension.Equals(
                    ".rtf",
                    StringComparison.OrdinalIgnoreCase),
                PlainTextTemplate => extension.Equals(
                    ".txt",
                    StringComparison.OrdinalIgnoreCase),
                _ => false
            };

        private static string NormalizeExtension(string extension)
        {
            if(string.IsNullOrWhiteSpace(extension))
                return string.Empty;
            return extension.StartsWith('.') ? extension : $".{extension}";
        }

        private static async Task<byte[]> ReadAllBytesAsync(
            Stream source,
            CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
    }
}

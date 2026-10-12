using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IDocumentTemplateService
{
    Task<byte[]> CreateContentAsync(DocumentTemplate template, IFileTemplate format,
        DocumentPaperSize paperSize, bool isLandscape, CancellationToken cancellationToken = default);
}

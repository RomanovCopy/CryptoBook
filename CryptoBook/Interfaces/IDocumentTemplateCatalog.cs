using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IDocumentTemplateCatalog
{
    IReadOnlyList<DocumentTemplate> Templates { get; }
    DocumentTemplate GetById(string? id);
}

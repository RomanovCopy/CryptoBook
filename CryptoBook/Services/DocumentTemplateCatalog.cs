using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services;

public sealed class DocumentTemplateCatalog: IDocumentTemplateCatalog
{
    public IReadOnlyList<DocumentTemplate> Templates { get; } = Array.AsReadOnly<DocumentTemplate>(
    [
        new(DocumentTemplate.EmptyId), new("notes"), new("plan"),
        new("meeting"), new("journal"), new("report")
    ]);

    public DocumentTemplate GetById(string? id) => Templates.FirstOrDefault(
        template => string.Equals(template.Id, id, StringComparison.Ordinal)) ?? Templates[0];
}

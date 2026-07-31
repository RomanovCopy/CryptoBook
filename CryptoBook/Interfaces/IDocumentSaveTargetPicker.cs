using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IDocumentSaveTargetPicker: IService
    {
        DocumentSaveTarget? Pick(
            string? currentFilePath,
            IFileTemplate? currentTemplate);
    }
}

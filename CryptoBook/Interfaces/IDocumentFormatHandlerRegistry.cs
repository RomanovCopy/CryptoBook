namespace CryptoBook.Interfaces
{
    public interface IDocumentFormatHandlerRegistry: IService
    {
        IDocumentFormatHandler? Find(IFileTemplate template);
    }
}

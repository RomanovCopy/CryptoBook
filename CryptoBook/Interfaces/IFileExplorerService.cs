namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Единая точка входа в файловый интерфейс CryptoBook.
    /// </summary>
    public interface IFileExplorerService: IService
    {
        void Show(string? initialDirectory = null);
    }
}

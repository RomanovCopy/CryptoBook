using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Единая точка входа в файловый интерфейс CryptoBook.
    /// </summary>
    public interface IFileExplorerService: IService
    {
        void Show(string? initialDirectory = null);

        /// <summary>
        /// Открывает FileExplorer для последовательного выбора файлов. После
        /// выбора окно остаётся открытым, а обработчику передаётся выбранный
        /// путь и снимок виртуального каталога «Все файлы», если он активен.
        /// </summary>
        void ShowFileSelection(
            string? initialDirectory,
            Action<MediaCatalogSelection> fileSelected);
    }
}

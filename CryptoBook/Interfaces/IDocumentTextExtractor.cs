using System.IO;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Извлекает доступный для поиска текст из одного или нескольких
    /// форматов. Новые форматы добавляются новой реализацией интерфейса.
    /// </summary>
    public interface IDocumentTextExtractor: IService
    {
        bool CanExtract(string extension);

        Task<string> ExtractAsync(
            Stream content,
            string extension,
            CancellationToken cancellationToken = default);
    }
}

using Drawing = System.Drawing;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Определяет исходные цвета содержимого документа независимо от темы интерфейса.
    /// </summary>
    public interface IDocumentAppearanceDefaults: IService
    {
        Drawing.Color PaperColor { get; }
        Drawing.Color TextColor { get; }
    }
}

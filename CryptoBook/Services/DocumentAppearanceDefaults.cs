using CryptoBook.Interfaces;

using Drawing = System.Drawing;

namespace CryptoBook.Services
{
    /// <summary>
    /// Единый источник исходных цветов нового документа.
    /// </summary>
    public sealed class DocumentAppearanceDefaults: IDocumentAppearanceDefaults
    {
        public Drawing.Color PaperColor => Drawing.Color.White;
        public Drawing.Color TextColor => Drawing.Color.Black;
    }
}

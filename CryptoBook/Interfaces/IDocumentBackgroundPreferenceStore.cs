using Drawing = System.Drawing;

namespace CryptoBook.Interfaces
{
    public interface IDocumentBackgroundPreferenceStore: IService
    {
        Drawing.Color? Load();
        void Save(Drawing.Color color);
    }
}

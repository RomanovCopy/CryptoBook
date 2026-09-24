using Drawing = System.Drawing;

namespace CryptoBook.Interfaces;

/// <summary>
/// Хранилище пользовательской палитры цветов чернил.
/// </summary>
public interface IInkPaletteStore
{
    IReadOnlyList<Drawing.Color> Load();

    void Save(IReadOnlyList<Drawing.Color> colors);
}

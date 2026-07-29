using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Адаптирует существующие панели форматирования для контекстного меню редактора.
    /// Не содержит собственной логики форматирования текста.
    /// </summary>
    public interface IRichTextContextMenuViewModel: IViewModel
    {
        IFontFormatBar_ViewModel FontFormatting { get; }
        ITextFormatBarViewModel TextFormatting { get; }
        IListFormatBarViewModel ListFormatting { get; }

        FontWeight Bold { get; }
        System.Windows.FontStyle Italic { get; }
        ITextDecorationItem? Underline { get; }

        ICommand ClearDocument { get; }
    }
}

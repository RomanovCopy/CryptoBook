using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
     interface ITitleBarRB_ViewModel: IViewModel
    {
        ICommand BoldCommand { get; }
        ICommand ItalicCommand { get; }
        ICommand UnderlineCommand { get; }
        ICommand ClearFormattingCommand { get; }
        ICommand InsertImageCommand { get; }
        ICommand ApplyFontFamily { get; }
        ICommand ApplyFontSize { get; }
        ICommand ApplyForegroundColor { get; }
        ICommand ApplyBackgroundColor { get; }
        ICommand ApplyTextAlignment {  get; }
        ICommand ApplyTextFormattingMode { get; }
        ICommand ApplyTextRenderingMode {  get; }
        ICommand ApplyAcceptsTab {  get; }
        ICommand ApplyAcceptsReturn { get; }
        ICommand ApplyVerticalScrollBarVisibility {  get; }

    }
}

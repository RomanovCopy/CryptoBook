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
        ICommand ApplyHorizontalScrollBarVisibility { get; }
        ICommand ClearFormatting { get; }
        ICommand SelectAll { get; }
        ICommand ClearSelection { get; }
        ICommand ReplaceSelectedText { get; }
        ICommand InsertHyperlink { get; }
        ICommand InsertParagraph { get; }
        ICommand InsertLineBreak { get; }
        ICommand InsertTable { get; }
        ICommand ClearDocument { get; }
        ICommand ScrollToCaret { get; }
        ICommand ScrollToEnd { get; }
        ICommand ScrollToStart { get; }
        ICommand SetDocumentMargin { get; }
        ICommand Undo { get; }
        ICommand Redo { get; }
        ICommand FindText { get; }
        ICommand ReplaceText { get; }
        ICommand ApplyBulletedList { get; }
        ICommand ApplyNumberedList { get; }
        ICommand RemoveListFormatting { get; }
        ICommand IncreaseIndent { get; }
        ICommand DecreaseIndent { get; }
        ICommand Focus { get; }
        ICommand InsertTextAtCaret { get; }
    }
}

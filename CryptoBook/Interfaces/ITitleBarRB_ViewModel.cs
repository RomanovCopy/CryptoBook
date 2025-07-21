using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface ITitleBarRB_ViewModel: IViewModel
    {
        public ICommand BoldCommand { get; }
        public ICommand ItalicCommand { get; }
        public ICommand UnderlineCommand { get; }
        public ICommand ClearFormattingCommand { get; }
        public ICommand InsertImageCommand { get; }
        public ICommand ApplyFontFamily { get; }
        public ICommand ApplyFontSize { get; }
        public ICommand ApplyForegroundColor { get; }
        public ICommand ApplyBackgroundColor { get; }
    }
}

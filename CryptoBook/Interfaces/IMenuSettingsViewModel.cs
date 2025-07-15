using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuSettingsViewModel
    {
        public ICommand SetFontWeight { get; }
        public ICommand SetFontFamily { get; }
        public ICommand SetFontSize { get; }
        public ICommand SetFontColor { get; }
        public ICommand SetFontBackColor { get; }
        public ICommand SetPaperColor { get; }
        public ICommand SetEncoding { get; }
        public ICommand Localization { get; }

    }
}

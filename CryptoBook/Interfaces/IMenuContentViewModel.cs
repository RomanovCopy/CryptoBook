using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMenuContentViewModel
    {
        public ICommand Reading { get; }
        public ICommand InsertImage { get; }
        public ICommand InsertText { get; }
        public ICommand OpenDocumentTree { get; }
        public ICommand MediaPlayer { get; }

    }
}

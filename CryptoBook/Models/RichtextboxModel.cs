using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Models
{
    public sealed class RichtextboxModel: ViewModelBase, IRichtextboxModel
    {
        public bool CanExecute_Loaded(object? obj) => true;
        public void Execute_Loaded(object? obj) { }

        public bool CanExecute_Close(object? obj) => true;
        public void Execute_Close(object? obj) { }

        public bool CanExecute_Closing(object? obj) => true;
        public void Execute_Closing(object? obj) { }

        public bool CanExecute_Closed(object? obj) => true;
        public void Execute_Closed(object? obj) { }
    }
}

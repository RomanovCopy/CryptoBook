using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    internal class RichtextboxModel: ViewModelBase
    {
        internal bool CanExecute_Loaded(object? obj) => true;
        internal void Execute_Loaded(object? obj) { }

        internal bool CanExecute_Close(object? obj) => true;
        internal void Execute_Close(object? obj) { }

        internal bool CanExecute_Closing(object? obj) => true;
        internal void Execute_Closing(object? obj) { }

        internal bool CanExecute_Closed(object? obj) => true;
        internal void Execute_Closed(object? obj) { }
    }
}

using System.ComponentModel;

namespace CryptoBook.Interfaces
{
    public interface IRichtextboxModel: IService, INotifyPropertyChanged
    {
        bool CanExecute_Loaded(object? parameter);
        void Execute_Loaded(object? parameter);
        bool CanExecute_Close(object? parameter);
        void Execute_Close(object? parameter);
        bool CanExecute_Closing(object? parameter);
        void Execute_Closing(object? parameter);
        bool CanExecute_Closed(object? parameter);
        void Execute_Closed(object? parameter);
    }
}

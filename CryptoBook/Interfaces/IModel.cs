using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IModel:INotifyPropertyChanged
    {
        bool CanExecute_Close(object? obj);
        void Execute_Loaded(object? obj);

        bool CanExecute_Loaded(object? obj);
        void Execute_Close(object? obj);

        bool CanExecute_Closing(object? obj);
        void Execute_Closing(object? obj);

        bool CanExecute_Closed(object? obj);
        void Execute_Closed(object? obj);
    }
}
